import Foundation
@testable import MomoCore

struct EnergyTests {
    private let start = Date(timeIntervalSince1970: 1_700_000_000)
    private func sample(_ second: Double, _ watts: Double?, _ scope: PowerScope = .system, source: String = "SMC PSTR") -> PowerSample {
        PowerSample(date: start.addingTimeInterval(second), uptime: second, watts: watts, scope: scope, source: source)
    }

    func testTrapezoidalEnergyAndCoverage() throws {
        var ledger = EnergyLedger()
        ledger.record(sample(0, 10))
        try expect(abs(ledger.record(sample(10, 30)) - 20 * 10 / 3600) < 1e-12)
        try expect(ledger.buckets[0].monitoredSeconds == 10)
    }
    func testMissingReadingDoesNotBecomeZeroOrBackfill() throws {
        var ledger = EnergyLedger()
        ledger.record(sample(0, 10)); ledger.record(sample(2, nil)); ledger.record(sample(4, 20))
        try expect(ledger.buckets.isEmpty)
        try expect(ledger.missingSeconds == 4)
        try expect(abs(ledger.record(sample(6, 20)) - 40 / 3600) < 1e-12)
    }
    func testValidZeroIsMonitored() throws {
        var ledger = EnergyLedger()
        ledger.record(sample(0, 0)); ledger.record(sample(2, 0))
        try expect(ledger.buckets[0].wattHours == 0)
        try expect(ledger.buckets[0].monitoredSeconds == 2)
    }
    func testScopeAndSourceTransitionsBreakIntegration() throws {
        var ledger = EnergyLedger()
        ledger.record(sample(0, 10)); ledger.record(sample(2, 20, .battery))
        ledger.record(sample(4, 20, .battery)); ledger.record(sample(6, 30, .battery, source: "IOKit BatteryPower"))
        try expect(ledger.buckets.count == 1)
        try expect(ledger.buckets[0].scope == .battery)
        try expect(ledger.buckets[0].monitoredSeconds == 2)
        try expect(ledger.missingSeconds == 4)
    }
    func testSleepGapAndResetNeverBackfill() throws {
        var ledger = EnergyLedger()
        ledger.record(sample(0, 50)); ledger.record(sample(3600, 50))
        try expect(ledger.buckets.isEmpty)
        ledger.resetBaseline(); ledger.record(sample(3602, 50))
        try expect(ledger.buckets.isEmpty)
    }
    func testWallClockJumpBreaksIntegration() throws {
        var ledger = EnergyLedger()
        ledger.record(sample(0, 10))
        var next = sample(2, 10); next.date = start.addingTimeInterval(3600)
        ledger.record(next)
        try expect(ledger.buckets.isEmpty)
    }
    func testMidnightSplitsEnergy() throws {
        var calendar = Calendar(identifier: .gregorian); calendar.timeZone = TimeZone(secondsFromGMT: 0)!
        let midnight = calendar.startOfDay(for: start).addingTimeInterval(86400)
        var a = sample(0, 36); a.date = midnight.addingTimeInterval(-5)
        var b = sample(10, 36); b.date = midnight.addingTimeInterval(5)
        var ledger = EnergyLedger(); ledger.record(a, calendar: calendar); ledger.record(b, calendar: calendar)
        try expect(ledger.buckets.count == 2)
        try expect(abs(ledger.buckets[0].wattHours - 0.05) < 1e-12)
        try expect(abs(ledger.buckets[1].wattHours - 0.05) < 1e-12)
    }
    func testInvalidValuesDoNotAccumulate() throws {
        for invalid in [Double.nan, Double.infinity, -1] {
            var ledger = EnergyLedger(); ledger.record(sample(0, 10)); ledger.record(sample(2, invalid))
            try expect(ledger.buckets.isEmpty)
        }
    }
    func testReloadDoesNotRestoreLiveBaseline() throws {
        var ledger = EnergyLedger(); ledger.record(sample(0, 10)); ledger.record(sample(2, 10))
        var reloaded = try JSONDecoder().decode(EnergyLedger.self, from: JSONEncoder().encode(ledger))
        let before = reloaded.buckets
        reloaded.record(sample(4, 10))
        try expect(reloaded.buckets == before)
    }
    func testSignedBatteryCurrent() throws {
        try expect(EnergyLedger.batteryWatts(millivolts: 12000, currentBits: UInt64(bitPattern: -1200)) == 14.4)
        try expect(EnergyLedger.batteryWatts(millivolts: 0, currentBits: 0) == nil)
    }
    func testTodayDoesNotMixScopes() throws {
        var ledger = EnergyLedger()
        ledger.record(sample(0, 10)); ledger.record(sample(2, 10))
        ledger.record(sample(4, 20, .dcInput)); ledger.record(sample(6, 20, .dcInput))
        try expect(abs(ledger.today(scope: .system, date: start).wattHours - 20 / 3600) < 1e-12)
        try expect(abs(ledger.today(scope: .dcInput, date: start).wattHours - 40 / 3600) < 1e-12)
    }
    func testTariffRequiresTheWholeInput() throws {
        try expect(try TariffInput.parse("0") == 0)
        try expect(try TariffInput.parse(" 2.75 ") == 2.75)
        try expect(try TariffInput.parse("2,75", decimalSeparator: ",") == 2.75)
        try expect(try TariffInput.parse("") == nil)
        for invalid in ["2junk", "-1", "1,000", "NaN", "1.2.3"] {
            do { _ = try TariffInput.parse(invalid); throw NSError(domain: "accepted invalid tariff", code: 1) }
            catch TariffInput.Invalid.price { }
        }
    }
    func testCorruptHistoryIsRejected() throws {
        let data = Data("{\"buckets\":[{\"day\":\"2026-10-07\",\"scope\":\"system\",\"wattHours\":-1,\"monitoredSeconds\":2}],\"missingSeconds\":0}".utf8)
        do { _ = try JSONDecoder().decode(EnergyLedger.self, from: data); throw NSError(domain: "accepted corrupt history", code: 1) }
        catch DecodingError.dataCorrupted { }
    }
}
