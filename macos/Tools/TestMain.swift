import Darwin
import Foundation

func expect(_ value: @autoclosure () throws -> Bool, file: String = #filePath, line: Int = #line) throws {
    if try !value() { throw NSError(domain: "MomoCoreChecks", code: 1, userInfo: [NSLocalizedDescriptionKey: "Assertion failed at \(file):\(line)"]) }
}

@main struct TestMain {
    static func main() {
        let suite = EnergyTests()
        let checks: [(String, () throws -> Void)] = [
            ("trapezoidal energy and coverage", suite.testTrapezoidalEnergyAndCoverage),
            ("missing readings never become zero", suite.testMissingReadingDoesNotBecomeZeroOrBackfill),
            ("valid zero remains monitored", suite.testValidZeroIsMonitored),
            ("scope and source changes break integration", suite.testScopeAndSourceTransitionsBreakIntegration),
            ("sleep and restart never backfill", suite.testSleepGapAndResetNeverBackfill),
            ("wall-clock changes break integration", suite.testWallClockJumpBreaksIntegration),
            ("midnight splits energy", suite.testMidnightSplitsEnergy),
            ("invalid values never accumulate", suite.testInvalidValuesDoNotAccumulate),
            ("history reload resets the live baseline", suite.testReloadDoesNotRestoreLiveBaseline),
            ("signed battery current", suite.testSignedBatteryCurrent),
            ("today keeps scopes separate", suite.testTodayDoesNotMixScopes),
            ("tariff validates the entire input", suite.testTariffRequiresTheWholeInput),
            ("corrupt history is rejected", suite.testCorruptHistoryIsRejected)
        ]
        var failures = 0
        for (name, run) in checks {
            do { try run(); print("PASS \(name)") }
            catch { failures += 1; print("FAIL \(name): \(error)") }
        }
        print("Core checks: \(checks.count - failures)/\(checks.count)")
        exit(failures == 0 ? 0 : 1)
    }
}
