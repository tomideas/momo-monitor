import Foundation

public struct MemoryReading: Codable, Sendable {
    public var usedBytes: UInt64?
    public var totalBytes: UInt64
    public init(usedBytes: UInt64?, totalBytes: UInt64) {
        self.usedBytes = usedBytes; self.totalBytes = totalBytes
    }
    public var percent: Double? {
        guard let usedBytes, totalBytes > 0, usedBytes <= totalBytes else { return nil }
        return Double(usedBytes) / Double(totalBytes) * 100
    }
}

public struct VolumeReading: Codable, Identifiable, Sendable {
    public var id: String
    public var name: String
    public var totalBytes: UInt64?
    public var availableBytes: UInt64?
    public init(id: String, name: String, totalBytes: UInt64?, availableBytes: UInt64?) {
        self.id = id; self.name = name; self.totalBytes = totalBytes; self.availableBytes = availableBytes
    }
    public var usedBytes: UInt64? {
        guard let totalBytes, let availableBytes, totalBytes > 0, availableBytes <= totalBytes else { return nil }
        return totalBytes - availableBytes
    }
    public var percent: Double? {
        guard let usedBytes, let totalBytes else { return nil }
        return Double(usedBytes) / Double(totalBytes) * 100
    }
}

public struct FanReading: Codable, Identifiable, Sendable {
    public var id: Int
    public var rpm: Double?
    public init(id: Int, rpm: Double?) { self.id = id; self.rpm = rpm }
}

public struct NetworkCounter: Sendable {
    public var received: UInt64
    public var sent: UInt64
    public init(received: UInt64, sent: UInt64) { self.received = received; self.sent = sent }
}

public struct NetworkRate: Codable, Sendable {
    public var receivedBytesPerSecond: Double
    public var sentBytesPerSecond: Double
    public var interfaces: [String]
}

/// Per-interface 64-bit deltas avoid spikes on reconnect, counter reset or sleep.
public struct NetworkRateTracker: Sendable {
    private var previous: (uptime: Double, counters: [String: NetworkCounter])?
    public init() {}
    public mutating func record(_ counters: [String: NetworkCounter]?, uptime: Double) -> NetworkRate? {
        guard let counters, uptime.isFinite, uptime >= 0 else { previous = nil; return nil }
        defer { previous = (uptime, counters) }
        guard let prior = previous else { return nil }
        let seconds = uptime - prior.uptime
        guard seconds > 0, seconds <= 15 else { return nil }
        var received = 0.0, sent = 0.0
        var interfaces: [String] = []
        for name in counters.keys.sorted() {
            let next = counters[name]!
            guard let before = prior.counters[name], next.received >= before.received, next.sent >= before.sent else { continue }
            received += Double(next.received - before.received)
            sent += Double(next.sent - before.sent)
            interfaces.append(name)
        }
        guard !interfaces.isEmpty else { return nil }
        return NetworkRate(receivedBytesPerSecond: received / seconds,
                           sentBytesPerSecond: sent / seconds, interfaces: interfaces)
    }
}

public struct HardwareSnapshot: Codable, Sendable {
    public var memory: MemoryReading?
    public var cpuTemperature: Double?
    public var temperatureSource: String
    public var fanCount: Int?
    public var fans: [FanReading]
    public var network: NetworkRate?
    public var volumes: [VolumeReading]
    public init(memory: MemoryReading? = nil, cpuTemperature: Double? = nil, temperatureSource: String = "",
                fanCount: Int? = nil, fans: [FanReading] = [], network: NetworkRate? = nil, volumes: [VolumeReading] = []) {
        self.memory = memory; self.cpuTemperature = cpuTemperature; self.temperatureSource = temperatureSource
        self.fanCount = fanCount; self.fans = fans; self.network = network; self.volumes = volumes
    }
}
