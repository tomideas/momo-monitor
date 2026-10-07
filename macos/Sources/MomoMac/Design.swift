import AppKit
import SwiftUI
import CoreText

enum Assets {
    static let directory: URL = {
        let executable = URL(fileURLWithPath: CommandLine.arguments[0]).deletingLastPathComponent()
        let candidates = [Bundle.main.resourceURL, Bundle.main.bundleURL, executable].compactMap { $0 }
        for candidate in candidates {
            if FileManager.default.fileExists(atPath: candidate.appendingPathComponent("DesignTokens.json").path) { return candidate }
            let bundleURL = candidate.appendingPathComponent("MomoMac_MomoMac.bundle")
            if let bundle = Bundle(url: bundleURL), let root = bundle.resourceURL {
                let assets = root.appendingPathComponent("Resources")
                if FileManager.default.fileExists(atPath: assets.appendingPathComponent("DesignTokens.json").path) { return assets }
            }
        }
        fatalError("Momo assets missing. Run macos/build.sh and launch the assembled .app.")
    }()

    static func registerFonts() {
        let fonts = directory.appendingPathComponent("Fonts")
        for path in (try? FileManager.default.contentsOfDirectory(at: fonts, includingPropertiesForKeys: nil)) ?? [] {
            CTFontManagerRegisterFontsForURL(path as CFURL, .process, nil)
        }
    }
    static func image(_ path: String) -> NSImage? { NSImage(contentsOf: directory.appendingPathComponent(path)) }
}

/// Values are read from a generated snapshot; the only editable owner remains
/// design-system/design-system.json. No independent native palette is stored here.
struct MomoTheme {
    private struct Document: Decodable {
        let tokens: [String: String]
        let themes: [String: [String: String]]
    }
    private static let document: Document = {
        let data = try! Data(contentsOf: Assets.directory.appendingPathComponent("DesignTokens.json"))
        return try! JSONDecoder().decode(Document.self, from: data)
    }()
    let mode: String
    func value(_ token: String) -> String {
        Self.document.themes[mode]?[token] ?? Self.document.tokens[token]!
    }
    func size(_ token: String) -> CGFloat { CGFloat(Double(value(token).replacingOccurrences(of: "px", with: ""))!) }
    func color(_ token: String) -> Color {
        let hex = value(token).trimmingCharacters(in: CharacterSet(charactersIn: "#"))
        let bits = UInt64(hex, radix: 16)!
        if hex.count == 8 {
            return Color(.sRGB, red: Double((bits >> 24) & 255) / 255,
                         green: Double((bits >> 16) & 255) / 255, blue: Double((bits >> 8) & 255) / 255,
                         opacity: Double(bits & 255) / 255)
        }
        return Color(.sRGB, red: Double((bits >> 16) & 255) / 255,
                     green: Double((bits >> 8) & 255) / 255, blue: Double(bits & 255) / 255, opacity: 1)
    }
    var paper: Color { color("--ds-bg-page") }
    var surface: Color { color("--ds-bg-surface") }
    var ink: Color { color("--ds-text-primary") }
    var muted: Color { color("--ds-text-secondary") }
    var accent: Color { color("--ds-accent") }
    var line: Color { color("--ds-border-subtle") }
    var danger: Color { color("--ds-danger") }
    var spacing: CGFloat { size("--ds-space-8") }
    var bodySize: CGFloat { size("--ds-font-size") }
    private static func family(_ token: String) -> String {
        document.tokens[token]!.components(separatedBy: ",")[0].trimmingCharacters(in: CharacterSet(charactersIn: "\" "))
    }
    static func body(_ size: CGFloat = 12) -> Font { .custom(family("--ds-font-ui"), size: size) }
    static func mono(_ size: CGFloat = 12) -> Font { .custom(family("--ds-font-mono"), size: size) }
    static func display(_ size: CGFloat) -> Font { .custom("BarlowCondensed-BlackItalic", size: size) }
}

struct MomoButtonStyle: ButtonStyle {
    let theme: MomoTheme
    var prominent = false
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(MomoTheme.body(theme.bodySize)).fontWeight(.medium)
            .padding(.horizontal, theme.size("--ds-space-7"))
            .frame(minHeight: theme.size("--ds-control-height"))
            .foregroundStyle(prominent ? theme.color("--ds-on-accent") : theme.ink)
            .background(prominent ? theme.accent : theme.surface)
            .clipShape(RoundedRectangle(cornerRadius: theme.size("--ds-radius-sm")))
            .opacity(configuration.isPressed ? 0.75 : 1)
            .contentShape(Rectangle())
    }
}

struct L10n {
    let language: String
    var chinese: Bool { language == "zh-Hant" }
    func text(_ zh: String, _ en: String) -> String { chinese ? zh : en }
}
