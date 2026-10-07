import Foundation

public enum PowerScope: String, Codable, CaseIterable, Sendable {
    case system, battery, dcInput
}

public struct PowerSample: Codable, Sendable {
    public var date: Date
    public var uptime: Double
    public var watts: Double?
    public var scope: PowerScope?
    public var source: String
    public var onAC: Bool?
    public var chargingWatts: Double?
    public var batteryPercent: Double?
    public var cpuPercent: Double?
    public var memoryPercent: Double?

    public init(date: Date = Date(), uptime: Double, watts: Double?, scope: PowerScope?,
                source: String, onAC: Bool? = nil, chargingWatts: Double? = nil,
                batteryPercent: Double? = nil, cpuPercent: Double? = nil, memoryPercent: Double? = nil) {
        self.date = date; self.uptime = uptime; self.watts = watts; self.scope = scope
        self.source = source; self.onAC = onAC; self.chargingWatts = chargingWatts
        self.batteryPercent = batteryPercent; self.cpuPercent = cpuPercent; self.memoryPercent = memoryPercent
    }

    public var hasValidPower: Bool {
        guard let watts, scope != nil, !source.isEmpty else { return false }
        return watts.isFinite && watts >= 0 && uptime.isFinite && uptime >= 0
    }
}

public struct EnergyBucket: Codable, Equatable, Sendable {
    public var day: String
    public var scope: PowerScope
    public var wattHours: Double
    public var monitoredSeconds: Double
}

public enum TariffInput {
    public enum Invalid: Error { case price }
    public static func parse(_ text: String, decimalSeparator: String = ".") throws -> Double? {
        let input = text.trimmingCharacters(in: .whitespacesAndNewlines)
        if input.isEmpty { return nil }
        let normalized = input.replacingOccurrences(of: decimalSeparator, with: ".")
        guard normalized.range(of: "^(?:[0-9]+(?:\\.[0-9]+)?|\\.[0-9]+)$", options: .regularExpression) != nil,
              let price = Double(normalized), price.isFinite else { throw Invalid.price }
        return price
    }
}

/// Same boundaries as Momo's Windows integrator: no backfilling, trapezoidal
/// integration, separate scopes and a fresh baseline after source/sleep changes.
public struct EnergyLedger: Codable, Sendable {
    public private(set) var buckets: [EnergyBucket] = []
    public private(set) var missingSeconds: Double = 0
    private var previous: PowerSample?

    private enum CodingKeys: String, CodingKey { case buckets, missingSeconds }
    public init() {}
    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        let loaded = try values.decode([EnergyBucket].self, forKey: .buckets)
        let missing = try values.decode(Double.self, forKey: .missingSeconds)
        guard missing.isFinite && missing >= 0 && loaded.allSatisfy({
            $0.wattHours.isFinite && $0.wattHours >= 0 && $0.monitoredSeconds.isFinite && $0.monitoredSeconds >= 0
                && $0.day.count == 10
        }) else { throw DecodingError.dataCorruptedError(forKey: .buckets, in: values, debugDescription: "Invalid energy history") }
        buckets = loaded; missingSeconds = missing
    }

    public mutating func resetBaseline() { previous = nil }

    @discardableResult
    public mutating func record(_ sample: PowerSample, calendar: Calendar = .current, maxGap: Double = 15) -> Double {
        guard sample.uptime.isFinite && sample.uptime >= 0 else { resetBaseline(); return 0 }
        defer { previous = sample }
        guard let prior = previous else { return 0 }
        let elapsed = sample.uptime - prior.uptime
        let wall = sample.date.timeIntervalSince(prior.date)
        guard elapsed > 0, elapsed <= maxGap, wall > 0, wall <= maxGap,
              abs(elapsed - wall) <= 1 else { return 0 }
        guard prior.hasValidPower, sample.hasValidPower, prior.scope == sample.scope,
              prior.source == sample.source, let scope = sample.scope else {
            missingSeconds += elapsed
            return 0
        }
        var cursor = prior.date
        var added = 0.0
        while cursor < sample.date {
            let nextMidnight = calendar.date(byAdding: .day, value: 1, to: calendar.startOfDay(for: cursor))!
            let end = min(sample.date, nextMidnight)
            let from = cursor.timeIntervalSince(prior.date) / wall
            let to = end.timeIntervalSince(prior.date) / wall
            let startW = prior.watts! + (sample.watts! - prior.watts!) * from
            let endW = prior.watts! + (sample.watts! - prior.watts!) * to
            let seconds = elapsed * (to - from)
            let wh = (startW + endW) / 2 * seconds / 3600
            let day = Self.dayKey(cursor, calendar: calendar)
            if let index = buckets.firstIndex(where: { $0.day == day && $0.scope == scope }) {
                buckets[index].wattHours += wh
                buckets[index].monitoredSeconds += seconds
            } else {
                buckets.append(EnergyBucket(day: day, scope: scope, wattHours: wh, monitoredSeconds: seconds))
            }
            added += wh; cursor = end
        }
        return added
    }

    public func today(scope: PowerScope, date: Date = Date(), calendar: Calendar = .current) -> EnergyBucket {
        let day = Self.dayKey(date, calendar: calendar)
        return buckets.first { $0.day == day && $0.scope == scope }
            ?? EnergyBucket(day: day, scope: scope, wattHours: 0, monitoredSeconds: 0)
    }

    public static func dayKey(_ date: Date, calendar: Calendar = .current) -> String {
        let c = calendar.dateComponents([.year, .month, .day], from: date)
        return String(format: "%04d-%02d-%02d", c.year!, c.month!, c.day!)
    }

    /// Registry battery current may be stored as UInt64 two's complement.
    public static func batteryWatts(millivolts: Double, currentBits: UInt64) -> Double? {
        guard millivolts.isFinite, millivolts > 0 else { return nil }
        let watts = abs(millivolts * Double(Int64(bitPattern: currentBits))) / 1_000_000
        return watts.isFinite && watts <= 2000 ? watts : nil
    }
}
