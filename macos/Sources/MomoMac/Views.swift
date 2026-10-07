import AppKit
import SwiftUI
import MomoCore

// Explicit alias selects the property wrapper on both macOS 15 and newer SDKs
// that also expose a same-named State macro.
private typealias FormState<Value> = SwiftUI.State<Value>

struct DashboardView: View {
    @ObservedObject var store: MonitorStore
    @ObservedObject var mascot: MascotPlayer
    let showMini: () -> Void
    let showSettings: () -> Void
    let quit: () -> Void
    var width: CGFloat? = 400
    private var theme: MomoTheme { store.theme }
    private var l: L10n { store.l10n }

    var body: some View {
        VStack(alignment: .leading, spacing: theme.spacing) {
            HStack(spacing: theme.size("--ds-space-7")) {
                MascotView(player: mascot, store: store, size: 56)
                VStack(alignment: .leading, spacing: 2) {
                    Text("Momo").font(MomoTheme.display(32)).foregroundStyle(theme.accent)
                    Text(l.text("你的 Mac，用電一眼看懂。", "Your Mac’s energy, at a glance.")).foregroundStyle(theme.muted)
                }
                Spacer(minLength: 0)
                Button(action: showSettings) { Image(systemName: "gearshape").font(.system(size: 16)) }
                    .buttonStyle(.plain).help(l.text("設定", "Settings"))
                    .accessibilityLabel(l.text("設定", "Settings"))
            }
            rule
            VStack(alignment: .leading, spacing: 4) {
                HStack {
                    Text(store.scopeLabel).fontWeight(.medium)
                    Spacer()
                    if let ac = store.sample.onAC, store.isFresh {
                        Label(ac ? l.text("插電", "Plugged in") : l.text("電池供電", "On battery"),
                              systemImage: ac ? "powerplug" : "battery.100")
                            .foregroundStyle(theme.muted)
                    }
                }
                HStack(alignment: .firstTextBaseline, spacing: 8) {
                    Text(store.wattsText).font(MomoTheme.display(92)).monospacedDigit()
                        .lineLimit(1).minimumScaleFactor(0.5)
                        .accessibilityLabel(store.scopeLabel + " " + store.wattsText + " W")
                    Text("W").font(MomoTheme.display(26)).foregroundStyle(theme.muted)
                    Spacer(minLength: 0)
                }.frame(height: 96, alignment: .leading)
                HStack(alignment: .top) {
                    Text(store.statusText).font(MomoTheme.mono(11)).foregroundStyle(theme.muted)
                        .fixedSize(horizontal: false, vertical: true)
                    Spacer(minLength: 4)
                    Button { store.retry() } label: { Image(systemName: "arrow.clockwise") }
                        .buttonStyle(.plain).help(l.text("重新讀取", "Retry sampling"))
                        .accessibilityLabel(l.text("重新讀取功耗", "Retry power reading"))
                }
            }
            HStack(alignment: .top, spacing: theme.spacing) {
                summary(l.text("今日用電", "Today’s energy"), value: store.today.wattHours.formatted(.number.precision(.fractionLength(2))), unit: "Wh")
                Rectangle().fill(theme.line).frame(width: 1, height: 44)
                VStack(alignment: .leading, spacing: 4) {
                    Text(l.text("估算電費", "Estimated cost")).foregroundStyle(theme.muted)
                    Button(action: showSettings) {
                        Text(store.costText).font(store.preferences.tariff == nil ? MomoTheme.body(14) : MomoTheme.display(28))
                            .monospacedDigit().foregroundStyle(store.preferences.tariff == nil ? theme.accent : theme.ink)
                    }.buttonStyle(.plain).help(l.text("設定每 kWh 電價", "Set price per kWh"))
                }.frame(maxWidth: .infinity, alignment: .leading)
            }
            Text(l.text("已監測", "Monitored") + " " + duration(store.today.monitoredSeconds))
                .font(MomoTheme.mono(11)).foregroundStyle(theme.muted)
            rule
            PowerTrendView(store: store)
            HStack(spacing: theme.spacing) {
                metric("CPU", value: store.sample.cpuPercent, symbol: "cpu")
                metric(l.text("記憶體", "Memory"), value: store.sample.memoryPercent, symbol: "memorychip")
            }
            if let battery = store.sample.batteryPercent, store.isFresh {
                HStack {
                    Label(l.text("電池", "Battery"), systemImage: "battery.100")
                    Text(battery.formatted(.number.precision(.fractionLength(0))) + "%").font(MomoTheme.mono())
                    Spacer()
                    if let charge = store.sample.chargingWatts {
                        Text(l.text("充電", "Charging") + " " + charge.formatted(.number.precision(.fractionLength(1))) + " W")
                            .font(MomoTheme.mono()).foregroundStyle(theme.muted)
                    }
                }
            }
            Text(store.scopeNote).font(MomoTheme.body(11)).foregroundStyle(theme.muted)
                .fixedSize(horizontal: false, vertical: true)
            if let error = store.storageError {
                Text(l.text("資料讀取或儲存失敗：", "Could not read or save data: ") + error).foregroundStyle(theme.danger)
                    .textSelection(.enabled).fixedSize(horizontal: false, vertical: true)
                Button(l.text("重試資料存取", "Retry data access")) { store.retryStorage() }
                    .buttonStyle(MomoButtonStyle(theme: theme))
            }
            rule
            HStack {
                Button(action: showMini) { Label(l.text("水豚浮窗", "Momo mini"), systemImage: "pip") }
                    .buttonStyle(MomoButtonStyle(theme: theme, prominent: true))
                Spacer()
                Button(l.text("結束", "Quit"), action: quit).buttonStyle(.plain).foregroundStyle(theme.muted)
                    .keyboardShortcut("q")
            }
        }
        .font(MomoTheme.body(theme.bodySize)).foregroundStyle(theme.ink)
        .padding(theme.size("--ds-space-10"))
        .frame(maxWidth: .infinity)
        .frame(width: width)
        .background(theme.paper)
        .tint(theme.accent)
        .preferredColorScheme(store.preferences.theme == "VOLT" ? .dark : .light)
    }
    private var rule: some View { Rectangle().fill(theme.line).frame(height: 1) }
    private func summary(_ label: String, value: String, unit: String) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(label).foregroundStyle(theme.muted)
            HStack(alignment: .firstTextBaseline, spacing: 4) {
                Text(value).font(MomoTheme.display(28)).monospacedDigit()
                Text(unit).font(MomoTheme.mono(11)).foregroundStyle(theme.muted)
            }
        }.frame(maxWidth: .infinity, alignment: .leading)
    }
    private func metric(_ label: String, value: Double?, symbol: String) -> some View {
        HStack {
            Label(label, systemImage: symbol).foregroundStyle(theme.muted)
            Spacer(minLength: 8)
            Text(store.isFresh ? value.map { $0.formatted(.number.precision(.fractionLength(0))) + "%" } ?? "—" : "—")
                .font(MomoTheme.mono()).monospacedDigit()
        }
        .frame(maxWidth: .infinity)
        .help(label == "CPU" ? l.text("兩次採樣間的整機 CPU 使用率", "System CPU usage between samples")
              : l.text("活躍、已鎖定與壓縮記憶體占實體記憶體比例", "Active, wired and compressed pages as a share of physical memory"))
    }
    private func duration(_ seconds: Double) -> String {
        let minutes = Int(seconds / 60)
        return minutes < 60 ? l.text("\(minutes) 分鐘", "\(minutes) min") : l.text("\(minutes / 60) 小時 \(minutes % 60) 分鐘", "\(minutes / 60) hr \(minutes % 60) min")
    }
}

struct PowerTrendView: View {
    @ObservedObject var store: MonitorStore
    var body: some View {
        let theme = store.theme
        let l = store.l10n
        let samples = store.trend
        let maximum = max(1, (samples.filter { $0.scope == store.scope }.compactMap(\.watts).max() ?? 1) * 1.15)
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text(l.text("功耗趨勢", "Power trend")).fontWeight(.medium)
                Spacer()
                Text(l.text("最近 5 分鐘 · W", "Last 5 minutes · W")).foregroundStyle(theme.muted)
            }
            if samples.filter({ $0.scope == store.scope && $0.hasValidPower }).count >= 2 {
                HStack(alignment: .top, spacing: 6) {
                    VStack { Text(maximum.formatted(.number.precision(.fractionLength(0)))); Spacer(); Text("0") }
                        .font(MomoTheme.mono(10)).foregroundStyle(theme.muted).frame(width: 30, height: 64, alignment: .trailing)
                    GeometryReader { geometry in
                        let end = samples.last?.date ?? Date()
                        ZStack {
                            VStack { Rectangle().fill(theme.line).frame(height: 1); Spacer(); Rectangle().fill(theme.line).frame(height: 1) }
                            Path { path in
                                var previous: PowerSample?
                                for sample in samples {
                                    guard sample.scope == store.scope, sample.hasValidPower, let watts = sample.watts else { previous = nil; continue }
                                    let point = CGPoint(x: geometry.size.width * max(0, 1 - end.timeIntervalSince(sample.date) / 300),
                                                        y: geometry.size.height * (1 - watts / maximum))
                                    if let prior = previous, prior.source == sample.source, sample.date.timeIntervalSince(prior.date) <= 15 {
                                        path.addLine(to: point)
                                    } else { path.move(to: point) }
                                    previous = sample
                                }
                            }.stroke(theme.accent, style: StrokeStyle(lineWidth: 2, lineCap: .round, lineJoin: .round))
                        }
                    }.frame(height: 64)
                }
                HStack { Text(l.text("5 分鐘前", "5 min ago")); Spacer(); Text(l.text("現在", "Now")) }
                    .font(MomoTheme.mono(10)).foregroundStyle(theme.muted)
            } else {
                Text(l.text("取得連續有效讀值後顯示趨勢。", "The trend appears after consecutive valid readings."))
                    .foregroundStyle(theme.muted).frame(height: 64, alignment: .center)
            }
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel(l.text("最近五分鐘的", "Last five minutes of ") + store.scopeLabel + l.text("趨勢，單位瓦。", ", in watts."))
    }
}

struct MiniView: View {
    @ObservedObject var store: MonitorStore
    @ObservedObject var mascot: MascotPlayer
    let restore: () -> Void
    let close: () -> Void
    let moved: () -> Void
    var body: some View {
        let theme = store.theme
        let l = store.l10n
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 8) {
                // The tracking overlay owns pointer gestures over the mascot; the
                // transparent native button underneath remains keyboard accessible.
                MascotView(player: mascot, store: store, size: 58).allowsHitTesting(false)
                VStack(alignment: .leading, spacing: 2) {
                    Text(store.scopeLabel).font(MomoTheme.body(11)).foregroundStyle(theme.muted)
                    HStack(alignment: .firstTextBaseline, spacing: 4) {
                        Text(store.wattsText).font(MomoTheme.display(36)).monospacedDigit().foregroundStyle(theme.accent)
                        Text("W").font(MomoTheme.display(14))
                    }
                }
                Spacer(minLength: 0)
                VStack(spacing: 10) {
                    Button(action: restore) { Image(systemName: "arrow.up.right") }
                        .help(l.text("開啟能耗面板", "Open energy panel")).accessibilityLabel(l.text("開啟能耗面板", "Open energy panel"))
                    Button(action: close) { Image(systemName: "xmark") }
                        .help(l.text("隱藏浮窗", "Hide mini")).accessibilityLabel(l.text("隱藏浮窗", "Hide mini"))
                }.buttonStyle(.plain).foregroundStyle(theme.muted)
            }
            Text(l.text("今日", "Today") + " " + store.today.wattHours.formatted(.number.precision(.fractionLength(2))) + " Wh")
                .font(MomoTheme.mono(12))
            HStack {
                Text("CPU " + (store.isFresh ? store.sample.cpuPercent.map { String(format: "%.0f%%", $0) } ?? "—" : "—"))
                Spacer()
                Text(l.text("記憶體 ", "MEM ") + (store.isFresh ? store.sample.memoryPercent.map { String(format: "%.0f%%", $0) } ?? "—" : "—"))
            }.font(MomoTheme.mono(11)).foregroundStyle(theme.muted)
        }
        .foregroundStyle(theme.ink).padding(theme.spacing).frame(width: 278)
        .background(theme.paper)
        .overlay(alignment: .topLeading) {
            // Keep restore/close controls outside the gesture target.
            MiniGestures(onHover: { mascot.play(reducedMotion: store.reducedMotion) },
                         onDrag: { mascot.play("mini-drag", reducedMotion: store.reducedMotion, priority: true) },
                         onRestore: restore, onMove: moved)
                .frame(width: 224, height: 154)
                .accessibilityHidden(true)
        }
        .clipShape(RoundedRectangle(cornerRadius: theme.size("--ds-radius-lg")))
        .overlay(RoundedRectangle(cornerRadius: theme.size("--ds-radius-lg")).stroke(theme.line, lineWidth: 1))
        .preferredColorScheme(store.preferences.theme == "VOLT" ? .dark : .light)
        .onAppear { mascot.play(reducedMotion: store.reducedMotion) }
        .onDisappear { mascot.stop() }
    }
}

struct SettingsView: View {
    @ObservedObject var store: MonitorStore
    let close: () -> Void
    @FormState private var draft: Preferences
    @FormState private var tariffText: String
    @FormState private var error: String?

    init(store: MonitorStore, close: @escaping () -> Void) {
        self.store = store; self.close = close
        _draft = FormState(initialValue: store.preferences)
        _tariffText = FormState(initialValue: store.preferences.tariff.map { String($0) } ?? "")
    }
    var body: some View {
        let l = store.l10n
        let theme = store.theme
        VStack(alignment: .leading, spacing: theme.spacing) {
            HStack {
                Text(l.text("設定", "Settings")).font(MomoTheme.body(20)).fontWeight(.bold)
                Spacer()
                Button(action: close) { Image(systemName: "xmark") }.buttonStyle(.plain)
                    .accessibilityLabel(l.text("取消設定", "Cancel settings"))
            }
            Form {
                Picker(l.text("外觀", "Appearance"), selection: $draft.theme) {
                    Text("PAPER POP").tag("PAPER POP"); Text("VOLT").tag("VOLT")
                }
                Picker(l.text("語言", "Language"), selection: $draft.language) {
                    Text("繁體中文").tag("zh-Hant"); Text("English").tag("en")
                }
                Toggle(l.text("浮窗保持置頂", "Keep mini on top"), isOn: $draft.keepOnTop)
                Toggle(l.text("減少動態效果", "Reduce motion"), isOn: $draft.reduceMotion)
                TextField(l.text("每 kWh 電價", "Price per kWh"), text: $tariffText)
                    .help(l.text("留空不計費；可填 0。", "Leave blank to disable cost; zero is valid."))
                TextField(l.text("貨幣符號", "Currency symbol"), text: $draft.currency)
            }
            .formStyle(.columns)
            .scrollContentBackground(.hidden)
            .padding(theme.spacing)
            .background(theme.surface)
            .clipShape(RoundedRectangle(cornerRadius: theme.size("--ds-radius-md")))
            Text(l.text("電費依目前量測範圍估算，不代表完整電力帳單。", "Cost is estimated for the displayed scope, not your full electricity bill."))
                .foregroundStyle(theme.muted).fixedSize(horizontal: false, vertical: true)
            if let error { Text(error).foregroundStyle(theme.danger).fixedSize(horizontal: false, vertical: true) }
            HStack {
                Button(l.text("取消", "Cancel"), action: close).keyboardShortcut(.cancelAction)
                    .buttonStyle(MomoButtonStyle(theme: theme))
                Spacer()
                Button(l.text("儲存設定", "Save settings")) { save() }.keyboardShortcut(.defaultAction)
                    .buttonStyle(MomoButtonStyle(theme: theme, prominent: true))
            }
            HStack(spacing: 4) {
                Text("Designed by Tom Tam ·")
                Link("tomideas.com", destination: URL(string: "https://tomideas.com")!)
                Spacer()
                Text("0.1.0")
            }.font(MomoTheme.body(10)).foregroundStyle(theme.muted)
        }
        .font(MomoTheme.body(theme.bodySize)).foregroundStyle(theme.ink)
        .padding(theme.size("--ds-space-10")).frame(width: 400).background(theme.paper)
        .tint(theme.accent).preferredColorScheme(store.preferences.theme == "VOLT" ? .dark : .light)
    }
    private func save() {
        let input = tariffText.trimmingCharacters(in: .whitespacesAndNewlines)
        if input.isEmpty { draft.tariff = nil }
        else {
            let formatter = NumberFormatter(); formatter.locale = Locale.current
            guard let tariff = try? TariffInput.parse(input, decimalSeparator: formatter.decimalSeparator ?? ".") else {
                error = store.l10n.text("請輸入有效的非負電價，或留空。", "Enter a valid nonnegative price, or leave blank."); return
            }
            draft.tariff = tariff
        }
        draft.currency = String(draft.currency.trimmingCharacters(in: .whitespacesAndNewlines).prefix(8))
        guard !draft.currency.isEmpty else {
            error = store.l10n.text("請輸入貨幣符號，例如 NT$ 或 $。", "Enter a currency symbol, such as NT$ or $."); return
        }
        if store.apply(draft) { close() }
        else { error = store.l10n.text("設定未儲存：", "Settings were not saved: ") + (store.storageError ?? "") }
    }
}
