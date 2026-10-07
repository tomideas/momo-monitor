import AppKit
import SwiftUI

@MainActor
final class MascotPlayer: ObservableObject {
    @Published private(set) var image: NSImage?
    private(set) var isPlaying = false
    private(set) var frameIndex = 65
    private var playback: Task<Void, Never>?
    private var cache: [String: ([NSImage], [Int])] = [:]

    init() { rest() }
    private func frames(_ clip: String) -> ([NSImage], [Int])? {
        if let cached = cache[clip] { return cached }
        let directory = Assets.directory.appendingPathComponent("Animations/" + clip)
        guard let data = try? Data(contentsOf: directory.appendingPathComponent("durations.json")),
              let durations = try? JSONDecoder().decode([Int].self, from: data), !durations.isEmpty,
              durations.allSatisfy({ $0 > 0 }) else { return nil }
        let frames = durations.indices.compactMap { NSImage(contentsOf: directory.appendingPathComponent(String(format: "%03d.png", $0))) }
        guard frames.count == durations.count else { return nil }
        cache[clip] = (frames, durations)
        return (frames, durations)
    }
    func play(_ clip: String = "mini-click", reducedMotion: Bool, priority: Bool = false) {
        guard !reducedMotion else { stop(); return }
        guard !isPlaying || priority, let (frames, durations) = frames(clip) else { return }
        playback?.cancel(); isPlaying = true; frameIndex = 0; image = frames[0]
        playback = Task { [weak self] in
            let start = ContinuousClock.now
            var elapsedMilliseconds = 0
            for index in frames.indices {
                guard !Task.isCancelled, let self else { return }
                self.frameIndex = index; self.image = frames[index]
                elapsedMilliseconds += durations[index]
                do { try await Task.sleep(until: start.advanced(by: .milliseconds(elapsedMilliseconds)), clock: .continuous) }
                catch { return }
            }
            guard !Task.isCancelled else { return }
            self?.isPlaying = false; self?.rest(); self?.playback = nil
        }
    }
    func stop() { playback?.cancel(); playback = nil; isPlaying = false; rest() }
    private func rest() {
        frameIndex = 65
        image = Assets.image("Animations/mini-click/065.png") ?? Assets.image("momo.png")
    }
}

struct MascotView: View {
    @ObservedObject var player: MascotPlayer
    let store: MonitorStore
    var size: CGFloat = 72
    var body: some View {
        Button { player.play(reducedMotion: store.reducedMotion) } label: {
            Group {
                if let image = player.image { Image(nsImage: image).resizable().interpolation(.high).scaledToFit() }
            }.frame(width: size, height: size)
        }
        .buttonStyle(.plain)
        .help(store.l10n.text("播放水豚動畫", "Play Momo animation"))
        .accessibilityLabel(store.l10n.text("播放水豚動畫", "Play Momo animation"))
    }
}

/// Native tracking preserves the Windows interaction: idle movement plays once,
/// drag takes priority, buttons own clicks, and double-click opens the dashboard.
final class MiniGestureView: NSView {
    var onHover: () -> Void = {}
    var onDrag: () -> Void = {}
    var onRestore: () -> Void = {}
    var onMove: () -> Void = {}
    private var dragOrigin: NSPoint?
    private var windowOrigin: NSPoint?
    private var dragging = false
    override var acceptsFirstResponder: Bool { true }
    override func updateTrackingAreas() {
        for area in trackingAreas { removeTrackingArea(area) }
        addTrackingArea(NSTrackingArea(rect: bounds, options: [.mouseEnteredAndExited, .mouseMoved, .activeAlways, .inVisibleRect], owner: self))
        super.updateTrackingAreas()
    }
    override func mouseEntered(with event: NSEvent) { if !dragging { onHover() } }
    override func mouseMoved(with event: NSEvent) { if !dragging { onHover() } }
    override func mouseDown(with event: NSEvent) {
        if event.clickCount == 2 { onRestore(); return }
        dragOrigin = NSEvent.mouseLocation; windowOrigin = window?.frame.origin; dragging = false
    }
    override func mouseDragged(with event: NSEvent) {
        guard let start = dragOrigin, let origin = windowOrigin else { return }
        let current = NSEvent.mouseLocation
        if !dragging, hypot(current.x - start.x, current.y - start.y) < 4 { return }
        if !dragging { dragging = true; onDrag() }
        window?.setFrameOrigin(NSPoint(x: origin.x + current.x - start.x, y: origin.y + current.y - start.y))
    }
    override func mouseUp(with event: NSEvent) {
        if dragging { onMove() }
        dragOrigin = nil; windowOrigin = nil; dragging = false
    }
}

struct MiniGestures: NSViewRepresentable {
    let onHover: () -> Void
    let onDrag: () -> Void
    let onRestore: () -> Void
    let onMove: () -> Void
    func makeNSView(context: Context) -> MiniGestureView { MiniGestureView() }
    func updateNSView(_ view: MiniGestureView, context: Context) {
        view.onHover = onHover; view.onDrag = onDrag; view.onRestore = onRestore; view.onMove = onMove
    }
}
