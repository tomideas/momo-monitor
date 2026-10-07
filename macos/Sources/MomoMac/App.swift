import AppKit
import SwiftUI
import MomoCore

@main
struct MomoEntry {
    static func main() {
        if CommandLine.arguments.contains("--probe") { runProbe(); return }
        let application = NSApplication.shared
        let delegate = AppController()
        application.delegate = delegate
        application.setActivationPolicy(.accessory)
        application.run()
        withExtendedLifetime(delegate) {}
    }

    private static func runProbe() {
        let arguments = CommandLine.arguments
        let count = arguments.firstIndex(of: "--samples").flatMap { i in i + 1 < arguments.count ? Int(arguments[i + 1]) : nil } ?? 5
        let reader = TelemetryReader()
        var ledger = EnergyLedger()
        let encoder = JSONEncoder(); encoder.dateEncodingStrategy = .iso8601; encoder.outputFormatting = [.sortedKeys]
        for index in 0..<min(120, max(1, count)) {
            let sample = reader.read(); ledger.record(sample)
            if let data = try? encoder.encode(sample), let text = String(data: data, encoding: .utf8) { print(text) }
            if index + 1 < count { Thread.sleep(forTimeInterval: 2) }
        }
        if let data = try? encoder.encode(ledger), let text = String(data: data, encoding: .utf8) { print(text) }
    }
}

@MainActor
final class AppController: NSObject, NSApplicationDelegate, NSPopoverDelegate, NSWindowDelegate {
    let verifying = CommandLine.arguments.contains("--verify-ui") || CommandLine.arguments.contains("--verification-session")
    lazy var store = MonitorStore(persistent: !verifying)
    let dashboardMascot = MascotPlayer()
    let miniMascot = MascotPlayer()
    private var statusItem: NSStatusItem!
    private let popover = NSPopover()
    private var mini: NSPanel?
    private var settings: NSPanel?

    func applicationDidFinishLaunching(_ notification: Notification) {
        Assets.registerFonts()
        NSApplication.shared.applicationIconImage = Assets.image("momo.png")
        statusItem = NSStatusBar.system.statusItem(withLength: 94)
        if let button = statusItem.button {
            if let image = Assets.image("momo.png") {
                let icon = image.copy() as! NSImage; icon.size = NSSize(width: 18, height: 18)
                button.image = icon; button.imagePosition = .imageLeft
            }
            button.font = NSFont.monospacedDigitSystemFont(ofSize: 12, weight: .medium)
            button.target = self; button.action = #selector(statusClicked(_:))
            button.sendAction(on: [.leftMouseUp, .rightMouseUp])
        }
        popover.behavior = .transient; popover.animates = false; popover.delegate = self
        store.onUpdate = { [weak self] in self?.updateChrome() }
        updateChrome(); store.start()
        if CommandLine.arguments.contains("--verify-ui") {
            Task { await verifyUI() }
        } else if !CommandLine.arguments.contains("--background") {
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.3) { [weak self] in self?.showDashboard(startup: true) }
        }
    }

    private func dashboard(width: CGFloat? = 400) -> DashboardView {
        DashboardView(store: store, mascot: dashboardMascot,
                      showMini: { [weak self] in self?.showMini() },
                      showSettings: { [weak self] in self?.showSettings() },
                      quit: { NSApplication.shared.terminate(nil) }, width: width)
    }
    private func updateChrome() {
        statusItem?.button?.title = " " + store.wattsText + " W"
        statusItem?.button?.toolTip = "Momo · " + store.scopeLabel + " · " + store.statusText
        statusItem?.button?.setAccessibilityLabel("Momo " + store.scopeLabel + " " + store.wattsText + " W")
        mini?.level = store.preferences.keepOnTop ? .floating : .normal
        if store.reducedMotion || store.sleeping { dashboardMascot.stop(); miniMascot.stop() }
    }
    @objc private func statusClicked(_ sender: Any?) {
        if NSApplication.shared.currentEvent?.type == .rightMouseUp { showMenu(); return }
        if popover.isShown { popover.performClose(nil) } else { showDashboard() }
    }
    func showDashboard(startup: Bool = false) {
        guard let button = statusItem.button else { return }
        prepareDashboard(for: button.window?.screen ?? NSScreen.main)
        NSApplication.shared.activate(ignoringOtherApps: true)
        popover.show(relativeTo: button.bounds, of: button, preferredEdge: .minY)
        dashboardMascot.play(startup ? "startup" : "mini-click", reducedMotion: store.reducedMotion)
    }
    private func prepareDashboard(for screen: NSScreen?, heightLimit: CGFloat? = nil) {
        // NSPopover positions from contentSize, not the hosting view's intrinsic
        // size. Its default 320×320 caused the taller dashboard to leave screen.
        let measurement = NSHostingView(rootView: dashboard())
        let naturalHeight = measurement.fittingSize.height
        let availableHeight = heightLimit ?? max(1, (screen?.visibleFrame.height ?? 800) - 48)
        let height = min(naturalHeight, availableHeight)
        popover.contentViewController = NSHostingController(rootView:
            ScrollView(.vertical) {
                dashboard(width: nil).fixedSize(horizontal: false, vertical: true)
            }
            .scrollBounceBehavior(.basedOnSize)
            .frame(width: 400, height: height)
            .background(store.theme.paper))
        popover.contentSize = NSSize(width: 400, height: height)
    }
    func popoverDidClose(_ notification: Notification) { dashboardMascot.stop() }

    func showMini() {
        popover.performClose(nil)
        if mini == nil {
            let panel = NSPanel(contentRect: NSRect(x: 0, y: 0, width: 278, height: 154),
                                styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
            panel.title = "Momo Mini"; panel.isOpaque = false; panel.backgroundColor = .clear
            panel.hasShadow = true; panel.hidesOnDeactivate = false; panel.isReleasedWhenClosed = false
            panel.collectionBehavior = [.fullScreenAuxiliary]
            panel.contentView = NSHostingView(rootView: MiniView(store: store, mascot: miniMascot,
                restore: { [weak self] in self?.hideMini(); self?.showDashboard() },
                close: { [weak self] in self?.hideMini() }, moved: { [weak self] in self?.saveMiniPosition() }))
            if let view = panel.contentView { panel.setContentSize(view.fittingSize) }
            let screen = NSScreen.main?.visibleFrame ?? NSRect(x: 0, y: 0, width: 1280, height: 800)
            var origin = NSPoint(x: screen.maxX - panel.frame.width - 24, y: screen.maxY - panel.frame.height - 60)
            if !verifying, let values = UserDefaults.standard.array(forKey: "MomoMiniOrigin") as? [Double], values.count == 2 {
                let saved = NSPoint(x: values[0], y: values[1])
                if saved.x.isFinite, saved.y.isFinite,
                   NSScreen.screens.contains(where: { $0.visibleFrame.contains(NSPoint(x: saved.x + 24, y: saved.y + 24)) }) {
                    origin = saved
                }
            }
            panel.setFrameOrigin(origin); mini = panel
        }
        mini?.level = store.preferences.keepOnTop ? .floating : .normal
        mini?.orderFrontRegardless()
        miniMascot.play(reducedMotion: store.reducedMotion)
    }
    private func hideMini() { miniMascot.stop(); mini?.orderOut(nil) }
    private func saveMiniPosition() {
        guard !verifying, let origin = mini?.frame.origin else { return }
        UserDefaults.standard.set([Double(origin.x), Double(origin.y)], forKey: "MomoMiniOrigin")
    }
    func showSettings() {
        popover.performClose(nil)
        if settings?.isVisible == true { settings?.makeKeyAndOrderFront(nil); return }
        let panel = NSPanel(contentRect: NSRect(x: 0, y: 0, width: 400, height: 400),
                            styleMask: [.titled, .closable], backing: .buffered, defer: false)
        panel.title = store.l10n.text("Momo 設定", "Momo Settings")
        panel.isReleasedWhenClosed = false; panel.delegate = self
        panel.contentView = NSHostingView(rootView: SettingsView(store: store, close: { [weak self] in self?.closeSettings() }))
        if let view = panel.contentView { panel.setContentSize(view.fittingSize) }
        panel.center(); settings = panel
        NSApplication.shared.activate(ignoringOtherApps: true); panel.makeKeyAndOrderFront(nil)
    }
    private func closeSettings() { settings?.close(); settings = nil }
    func windowWillClose(_ notification: Notification) { if notification.object as? NSWindow === settings { settings = nil } }

    private func showMenu() {
        let l = store.l10n
        let menu = NSMenu()
        for (title, action) in [(l.text("開啟能耗面板", "Open energy panel"), #selector(menuDashboard)),
                                (l.text("水豚浮窗", "Momo mini"), #selector(menuMini)),
                                (l.text("設定", "Settings"), #selector(menuSettings))] {
            let item = NSMenuItem(title: title, action: action, keyEquivalent: ""); item.target = self; menu.addItem(item)
        }
        menu.addItem(.separator())
        let quit = NSMenuItem(title: l.text("結束 Momo", "Quit Momo"), action: #selector(menuQuit), keyEquivalent: "q")
        quit.target = self; menu.addItem(quit)
        statusItem.menu = menu; statusItem.button?.performClick(nil); statusItem.menu = nil
    }
    @objc private func menuDashboard() { showDashboard() }
    @objc private func menuMini() { showMini() }
    @objc private func menuSettings() { showSettings() }
    @objc private func menuQuit() { NSApplication.shared.terminate(nil) }
    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        store.stop(); dashboardMascot.stop(); miniMascot.stop(); return .terminateNow
    }

    /// Capture real AppKit/SwiftUI windows using live samples; no user data is saved.
    private func verifyUI() async {
        let args = CommandLine.arguments
        guard let index = args.firstIndex(of: "--verify-ui"), index + 1 < args.count else { NSApplication.shared.terminate(nil); return }
        let directory = URL(fileURLWithPath: args[index + 1], isDirectory: true)
        try? FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        var assertions: [String: Bool] = [:]
        try? await Task.sleep(nanoseconds: 4_500_000_000)
        assertions["liveSampling"] = store.hasSample
        assertions["powerValidOrExplicitlyMissing"] = store.sample.hasValidPower || store.wattsText == "—"
        assertions["liveEnergyAccumulated"] = !store.sample.hasValidPower || store.today.monitoredSeconds >= 2
        showDashboard()
        try? await Task.sleep(nanoseconds: 500_000_000)
        if let view = popover.contentViewController?.view, let window = view.window {
            print("Popover content: \(view.bounds), window: \(window.frame), visible screen: \(window.screen?.visibleFrame ?? .zero), content size: \(popover.contentSize)")
            let contentFrame = window.convertToScreen(view.convert(view.bounds, to: nil))
            assertions["popoverWithinScreen"] = window.screen?.visibleFrame.contains(contentFrame) == true
            assertions["popoverSizeMatchesContent"] = abs(view.bounds.height - popover.contentSize.height) < 1
            assertions["popoverScreenshot"] = capturePNG(view, to: directory.appendingPathComponent("popover.png"))
            assertions["popoverMascotVisible"] = dashboardMascot.image != nil
        } else { assertions["popoverWithinScreen"] = false }
        popover.performClose(nil)
        if let button = statusItem.button {
            prepareDashboard(for: button.window?.screen, heightLimit: 440)
            popover.show(relativeTo: button.bounds, of: button, preferredEdge: .minY)
            try? await Task.sleep(nanoseconds: 300_000_000)
            if let view = popover.contentViewController?.view, let window = view.window {
                let contentFrame = window.convertToScreen(view.convert(view.bounds, to: nil))
                assertions["shortPopoverWithinScreen"] = window.screen?.visibleFrame.contains(contentFrame) == true
                assertions["shortPopoverBounded"] = abs(view.bounds.height - 440) < 1
                assertions["shortPopoverScreenshot"] = capturePNG(view, to: directory.appendingPathComponent("popover-short.png"))
                if let scroll = findScrollView(in: view), let document = scroll.documentView {
                    let bottom = max(0, document.bounds.height - scroll.contentView.bounds.height)
                    scroll.contentView.scroll(to: NSPoint(x: 0, y: bottom))
                    scroll.reflectScrolledClipView(scroll.contentView)
                    try? await Task.sleep(nanoseconds: 200_000_000)
                    assertions["shortPopoverScrollsToBottom"] = bottom > 0 && scroll.documentVisibleRect.maxY >= document.bounds.maxY - 1
                    assertions["shortPopoverFooterScreenshot"] = capturePNG(view, to: directory.appendingPathComponent("popover-short-bottom.png"))
                } else { assertions["shortPopoverScrollsToBottom"] = false }
            } else { assertions["shortPopoverWithinScreen"] = false }
            popover.performClose(nil)
        }
        let capture = NSPanel(contentRect: NSRect(x: 0, y: 0, width: 400, height: 600), styleMask: [.titled], backing: .buffered, defer: false)
        for (mode, language, name) in [("PAPER POP", "en", "paper-en"), ("VOLT", "zh-Hant", "volt-zh")] {
            var preferences = store.preferences; preferences.theme = mode; preferences.language = language; preferences.reduceMotion = true
            _ = store.apply(preferences)
            capture.contentView = NSHostingView(rootView: dashboard())
            capture.setContentSize(capture.contentView!.fittingSize); capture.center(); capture.orderFrontRegardless()
            try? await Task.sleep(nanoseconds: 400_000_000)
            assertions[name + "Screenshot"] = capturePNG(capture.contentView!, to: directory.appendingPathComponent(name + ".png"))
            assertions[name + "Width"] = abs(capture.contentView!.bounds.width - 400) < 1
            showMini(); miniMascot.stop()
            try? await Task.sleep(nanoseconds: 200_000_000)
            assertions[name + "MiniVisible"] = mini?.isVisible == true && mini?.screen?.visibleFrame.contains(mini!.frame) == true
            assertions[name + "MiniScreenshot"] = capturePNG(mini!.contentView!, to: directory.appendingPathComponent(name + "-mini.png"))
            hideMini(); assertions[name + "HiddenStopsAnimation"] = !miniMascot.isPlaying
            showSettings()
            try? await Task.sleep(nanoseconds: 200_000_000)
            assertions[name + "SettingsScreenshot"] = capturePNG(settings!.contentView!, to: directory.appendingPathComponent(name + "-settings.png"))
            closeSettings()
        }
        var preferences = store.preferences; preferences.reduceMotion = false; _ = store.apply(preferences)
        miniMascot.play(reducedMotion: false)
        try? await Task.sleep(nanoseconds: 450_000_000)
        assertions["framesAdvance"] = miniMascot.frameIndex > 0 && miniMascot.isPlaying
        let frame = miniMascot.frameIndex
        miniMascot.play(reducedMotion: false)
        assertions["hoverDoesNotRestart"] = miniMascot.frameIndex == frame
        miniMascot.play("mini-drag", reducedMotion: false, priority: true)
        assertions["dragTakesPriority"] = miniMascot.isPlaying && miniMascot.frameIndex == 0
        try? await Task.sleep(nanoseconds: 6_800_000_000)
        assertions["oneShotStops"] = !miniMascot.isPlaying && miniMascot.frameIndex == 65
        miniMascot.play(reducedMotion: true)
        assertions["reducedMotionStatic"] = !miniMascot.isPlaying
        let dataChecks = directory.appendingPathComponent("temporary-data-checks")
        let persistence = MonitorStore(directory: dataChecks)
        var saved = Preferences(); saved.theme = "VOLT"; saved.language = "zh-Hant"; saved.tariff = 2.75
        assertions["preferencesSave"] = persistence.apply(saved)
        let time = Date()
        persistence.ledger.record(PowerSample(date: time, uptime: 0, watts: 10, scope: .system, source: "test"))
        persistence.ledger.record(PowerSample(date: time.addingTimeInterval(2), uptime: 2, watts: 10, scope: .system, source: "test"))
        persistence.saveHistory(); persistence.stop()
        let restored = MonitorStore(directory: dataChecks)
        assertions["preferencesReload"] = restored.preferences == saved
        assertions["historyReload"] = restored.ledger.buckets.count == 1 && restored.ledger.buckets[0].wattHours > 0
        restored.stop()
        let brokenData = Data("broken history".utf8)
        let historyURL = dataChecks.appendingPathComponent("energy.json")
        try? brokenData.write(to: historyURL, options: .atomic)
        let broken = MonitorStore(directory: dataChecks)
        broken.saveHistory()
        assertions["corruptHistoryPreserved"] = broken.storageError != nil && (try? Data(contentsOf: historyURL)) == brokenData
        broken.stop()
        try? FileManager.default.removeItem(at: dataChecks)
        store.pauseForSleep(); assertions["sleepPauses"] = store.sleeping && store.wattsText == "—"
        store.resumeFromSleep(); assertions["wakeResets"] = !store.sleeping
        if let data = try? JSONSerialization.data(withJSONObject: assertions, options: [.prettyPrinted, .sortedKeys]) {
            try? data.write(to: directory.appendingPathComponent("native-checks.json"), options: .atomic)
        }
        print("Native UI checks: \(assertions.values.filter { $0 }.count)/\(assertions.count)")
        capture.close(); NSApplication.shared.terminate(nil)
    }
    private func capturePNG(_ view: NSView, to url: URL) -> Bool {
        view.layoutSubtreeIfNeeded()
        guard let bitmap = view.bitmapImageRepForCachingDisplay(in: view.bounds) else { return false }
        view.cacheDisplay(in: view.bounds, to: bitmap)
        guard let data = bitmap.representation(using: .png, properties: [:]) else { return false }
        do { try data.write(to: url, options: .atomic); return true } catch { return false }
    }
    private func findScrollView(in view: NSView) -> NSScrollView? {
        if let scroll = view as? NSScrollView { return scroll }
        for child in view.subviews { if let scroll = findScrollView(in: child) { return scroll } }
        return nil
    }
}
