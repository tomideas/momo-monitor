import AppKit
import Combine
import Foundation
import MomoCore

struct Preferences: Codable, Equatable {
    var theme = "PAPER POP"
    var language = Locale.preferredLanguages.first?.hasPrefix("zh") == true ? "zh-Hant" : "en"
    var reduceMotion = false
    var keepOnTop = false
    var tariff: Double? = nil
    var currency = "$"
}

@MainActor
final class MonitorStore: ObservableObject {
    @Published var sample = PowerSample(uptime: 0, watts: nil, scope: nil, source: "")
    @Published var trend: [PowerSample] = []
    @Published var preferences = Preferences()
    @Published var storageError: String?
    @Published var ledger = EnergyLedger()
    @Published var sleeping = false
    @Published var hasSample = false
    private let queue = DispatchQueue(label: "com.tomideas.momo.telemetry", qos: .utility)
    // Confined to the serial telemetry queue, never accessed by UI code.
    nonisolated(unsafe) private var reader: TelemetryReader?
    private var timer: DispatchSourceTimer?
    private var sampling = false
    private var generation = 0
    private var lastSave = Date.distantPast
    private var historyReadError: String?
    private var preferencesReadError: String?
    private var observers: [NSObjectProtocol] = []
    private let persistent: Bool
    private let dataDirectory: URL
    var onUpdate: (() -> Void)?

    init(persistent: Bool = true, directory: URL? = nil) {
        self.persistent = persistent
        dataDirectory = directory ?? FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("MomoMonitorMac", isDirectory: true)
        if persistent {
            do {
                try FileManager.default.createDirectory(at: dataDirectory, withIntermediateDirectories: true)
                if FileManager.default.fileExists(atPath: dataDirectory.appendingPathComponent("preferences.json").path) {
                    preferences = try JSONDecoder().decode(Preferences.self, from: Data(contentsOf: dataDirectory.appendingPathComponent("preferences.json")))
                    if !["PAPER POP", "VOLT"].contains(preferences.theme) { preferences.theme = "PAPER POP" }
                    if !["zh-Hant", "en"].contains(preferences.language) { preferences.language = "en" }
                    if let tariff = preferences.tariff, !tariff.isFinite || tariff < 0 { preferences.tariff = nil }
                }
            } catch { preferencesReadError = String(describing: error); storageError = preferencesReadError }
            do {
                if FileManager.default.fileExists(atPath: dataDirectory.appendingPathComponent("energy.json").path) {
                    ledger = try JSONDecoder().decode(EnergyLedger.self, from: Data(contentsOf: dataDirectory.appendingPathComponent("energy.json")))
                }
            } catch { historyReadError = String(describing: error); storageError = historyReadError }
        }
        let center = NSWorkspace.shared.notificationCenter
        observers.append(center.addObserver(forName: NSWorkspace.willSleepNotification, object: nil, queue: .main) { [weak self] _ in
            Task { @MainActor in self?.pauseForSleep() }
        })
        observers.append(center.addObserver(forName: NSWorkspace.didWakeNotification, object: nil, queue: .main) { [weak self] _ in
            Task { @MainActor in self?.resumeFromSleep() }
        })
        observers.append(NotificationCenter.default.addObserver(forName: .NSSystemTimeZoneDidChange, object: nil, queue: .main) { [weak self] _ in
            Task { @MainActor in self?.ledger.resetBaseline() }
        })
    }

    var l10n: L10n { L10n(language: preferences.language) }
    var theme: MomoTheme { MomoTheme(mode: preferences.theme) }
    var reducedMotion: Bool {
        preferences.reduceMotion || NSWorkspace.shared.accessibilityDisplayShouldReduceMotion || ProcessInfo.processInfo.isLowPowerModeEnabled
    }
    var scope: PowerScope { sample.scope ?? .system }
    var today: EnergyBucket { ledger.today(scope: scope) }
    var isFresh: Bool { hasSample && !sleeping && Date().timeIntervalSince(sample.date) < 15 }
    var wattsText: String {
        guard isFresh, sample.hasValidPower, let watts = sample.watts else { return "—" }
        return watts.formatted(.number.precision(.fractionLength(1)))
    }
    var scopeLabel: String {
        switch sample.scope {
        case .system: return l10n.text("系統功耗", "System power")
        case .battery: return l10n.text("電池端功耗", "Battery power")
        case .dcInput: return l10n.text("DC 輸入功耗", "DC input power")
        case nil: return l10n.text("目前功耗", "Current power")
        }
    }
    var scopeNote: String {
        switch scope {
        case .system: return l10n.text("系統端讀值；不含完整插座用電與外接螢幕。", "System-side reading; excludes full wall power and external displays.")
        case .battery: return l10n.text("電池放電範圍；不含之後充電的轉換損耗。", "Battery discharge; excludes losses during later charging.")
        case .dcInput: return l10n.text("電腦的 DC 輸入，可能包含充電；不等於插座瓦數。", "DC input may include charging; this is not wall power.")
        }
    }
    var statusText: String {
        if sleeping { return l10n.text("睡眠期間暫停監測", "Monitoring paused for sleep") }
        if !hasSample { return l10n.text("正在讀取功耗…", "Reading power…") }
        if !isFresh { return l10n.text("讀值已過期，請重新讀取。", "Reading is stale. Retry sampling.") }
        if !sample.hasValidPower { return l10n.text("此機型暫未提供可用功耗；可重新讀取。", "No supported power reading on this Mac. You can retry.") }
        return sample.source
    }
    var costText: String {
        guard let tariff = preferences.tariff else { return l10n.text("設定電價", "Set tariff") }
        return preferences.currency + (today.wattHours / 1000 * tariff).formatted(.number.precision(.fractionLength(3)))
    }

    func start() {
        guard timer == nil else { return }
        let timer = DispatchSource.makeTimerSource(queue: .main)
        timer.schedule(deadline: .now(), repeating: 2, leeway: .milliseconds(200))
        timer.setEventHandler { [weak self] in Task { @MainActor in self?.refresh() } }
        self.timer = timer; timer.resume()
    }
    func refresh() {
        guard !sampling, !sleeping else { return }
        sampling = true
        let generation = self.generation
        queue.async { [weak self] in
            guard let self else { return }
            if self.reader == nil { self.reader = TelemetryReader() }
            let result = self.reader!.read()
            Task { @MainActor in
                self.sampling = false
                guard generation == self.generation, !self.sleeping else { return }
                self.sample = result; self.hasSample = true
                self.ledger.record(result)
                self.trend.append(result)
                self.trend.removeAll { result.date.timeIntervalSince($0.date) > 300 }
                if Date().timeIntervalSince(self.lastSave) >= 30 { self.saveHistory() }
                self.onUpdate?()
            }
        }
    }
    func retry() {
        generation += 1
        ledger.resetBaseline()
        queue.async { [weak self] in self?.reader = nil }
        refresh()
    }
    func pauseForSleep() {
        sleeping = true; generation += 1; ledger.resetBaseline(); saveHistory(); onUpdate?()
    }
    func resumeFromSleep() {
        sleeping = false; generation += 1; ledger.resetBaseline(); refresh()
    }
    func apply(_ preferences: Preferences) -> Bool {
        do {
            if persistent { try JSONEncoder().encode(preferences).write(to: dataDirectory.appendingPathComponent("preferences.json"), options: .atomic) }
            self.preferences = preferences; preferencesReadError = nil; storageError = historyReadError; onUpdate?(); return true
        } catch { storageError = String(describing: error); return false }
    }
    func saveHistory() {
        lastSave = Date()
        guard persistent else { return }
        // Preserve unreadable history rather than silently replacing it with zeros.
        if let historyReadError { storageError = historyReadError; return }
        do {
            try JSONEncoder().encode(ledger).write(to: dataDirectory.appendingPathComponent("energy.json"), options: .atomic)
            storageError = preferencesReadError
        } catch { storageError = String(describing: error) }
    }
    func retryStorage() {
        if historyReadError != nil {
            do {
                let restored = try JSONDecoder().decode(EnergyLedger.self, from: Data(contentsOf: dataDirectory.appendingPathComponent("energy.json")))
                ledger = restored; ledger.resetBaseline(); historyReadError = nil
            } catch { historyReadError = String(describing: error); storageError = historyReadError; return }
        }
        saveHistory()
    }
    func stop() {
        timer?.cancel(); timer = nil; generation += 1; saveHistory()
        for observer in observers {
            NSWorkspace.shared.notificationCenter.removeObserver(observer)
            NotificationCenter.default.removeObserver(observer)
        }
        observers.removeAll()
    }
}
