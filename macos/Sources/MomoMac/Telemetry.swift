import Foundation
import IOKit
import IOKit.ps
import Darwin
import CMomoSMC
import MomoCore

/// All access is read-only and serial on the monitor queue. No sudo or helper process.
final class TelemetryReader {
    private var smc = momo_smc_open()
    private var battery: io_service_t = 0
    private var previousCPU: [UInt32]?

    init() {
        battery = IOServiceGetMatchingService(kIOMainPortDefault, IOServiceMatching("AppleSmartBattery"))
    }
    deinit {
        momo_smc_close(smc)
        if battery != 0 { IOObjectRelease(battery) }
    }

    func read() -> PowerSample {
        let power = powerState()
        let telemetry = property("PowerTelemetryData") as? [String: Any] ?? [:]
        var watts: Double?
        var scope: PowerScope?
        var source = ""
        // PSTR and SystemLoad describe the system boundary, not wall/charger wattage.
        if let value = smcWatts("PSTR") {
            watts = value; scope = .system; source = "SMC PSTR"
        } else if let value = validMilliwatts(telemetry["SystemLoad"]) {
            watts = value; scope = .system; source = "IOKit SystemLoad"
        } else if power.onAC == false {
            if let value = validMilliwatts(telemetry["BatteryPower"], magnitude: true) {
                watts = value; scope = .battery; source = "IOKit BatteryPower"
            } else if let voltage = property("Voltage") as? NSNumber,
                      let current = (property("InstantAmperage") ?? property("Amperage")) as? NSNumber,
                      let value = EnergyLedger.batteryWatts(millivolts: voltage.doubleValue, currentBits: current.uint64Value) {
                watts = value; scope = .battery; source = "IOKit voltage × current"
            }
        }
        // DC input includes charging and is a different boundary. Never relabel it as system load.
        if watts == nil, power.onAC == true, let value = smcWatts("PDTR") {
            watts = value; scope = .dcInput; source = "SMC PDTR"
        }
        if watts == nil, power.onAC == true, let value = validMilliwatts(telemetry["SystemPowerIn"]) {
            watts = value; scope = .dcInput; source = "IOKit SystemPowerIn"
        }
        let charging = power.charging == true ? validMilliwatts(telemetry["BatteryPower"], magnitude: true) : nil
        return PowerSample(uptime: ProcessInfo.processInfo.systemUptime, watts: watts, scope: scope,
                           source: source, onAC: power.onAC, chargingWatts: charging,
                           batteryPercent: power.percent, cpuPercent: cpuUsage(), memoryPercent: memoryUsage())
    }

    private func property(_ key: String) -> Any? {
        guard battery != 0 else { return nil }
        return IORegistryEntryCreateCFProperty(battery, key as CFString, kCFAllocatorDefault, 0)?.takeRetainedValue()
    }
    private func smcWatts(_ key: String) -> Double? {
        var watts = 0.0
        return key.withCString { momo_smc_read_watts(smc, $0, &watts) == 1 ? watts : nil }
    }
    private func validMilliwatts(_ value: Any?, magnitude: Bool = false) -> Double? {
        guard let number = value as? NSNumber else { return nil }
        let raw = magnitude ? abs(Double(Int64(bitPattern: number.uint64Value))) : number.doubleValue
        let watts = raw / 1000
        return watts.isFinite && watts >= 0 && watts <= 2000 ? watts : nil
    }

    private func powerState() -> (onAC: Bool?, charging: Bool?, percent: Double?) {
        guard let info = IOPSCopyPowerSourcesInfo()?.takeRetainedValue(),
              let sources = IOPSCopyPowerSourcesList(info)?.takeRetainedValue() as? [CFTypeRef] else {
            return (nil, nil, nil)
        }
        let onAC = (IOPSGetProvidingPowerSourceType(info)?.takeUnretainedValue() as String?) == kIOPSACPowerValue
        for source in sources {
            guard let data = IOPSGetPowerSourceDescription(info, source)?.takeUnretainedValue() as? [String: Any],
                  data[kIOPSTypeKey] as? String == kIOPSInternalBatteryType else { continue }
            let current = (data[kIOPSCurrentCapacityKey] as? NSNumber)?.doubleValue
            let maximum = (data[kIOPSMaxCapacityKey] as? NSNumber)?.doubleValue
            let percent: Double? = if let current, let maximum, maximum > 0 { min(100, max(0, current / maximum * 100)) } else { nil }
            return (onAC, data[kIOPSIsChargingKey] as? Bool, percent)
        }
        return (onAC, false, nil) // A desktop has no battery row.
    }

    private func cpuUsage() -> Double? {
        var info = host_cpu_load_info()
        var count = mach_msg_type_number_t(MemoryLayout<host_cpu_load_info>.size / MemoryLayout<integer_t>.size)
        let result = withUnsafeMutablePointer(to: &info) { p in
            p.withMemoryRebound(to: integer_t.self, capacity: Int(count)) {
                host_statistics(mach_host_self(), HOST_CPU_LOAD_INFO, $0, &count)
            }
        }
        guard result == KERN_SUCCESS else { return nil }
        let current = [info.cpu_ticks.0, info.cpu_ticks.1, info.cpu_ticks.2, info.cpu_ticks.3]
        defer { previousCPU = current }
        guard let previous = previousCPU else { return nil }
        let delta = zip(current, previous).map { Double($0 &- $1) }
        let total = delta.reduce(0, +)
        return total > 0 ? min(100, max(0, (total - delta[Int(CPU_STATE_IDLE)]) / total * 100)) : nil
    }

    private func memoryUsage() -> Double? {
        var info = vm_statistics64()
        var count = mach_msg_type_number_t(MemoryLayout<vm_statistics64>.size / MemoryLayout<integer_t>.size)
        let result = withUnsafeMutablePointer(to: &info) { p in
            p.withMemoryRebound(to: integer_t.self, capacity: Int(count)) {
                host_statistics64(mach_host_self(), HOST_VM_INFO64, $0, &count)
            }
        }
        guard result == KERN_SUCCESS else { return nil }
        let pages = Double(info.active_count) + Double(info.wire_count) + Double(info.compressor_page_count)
        return min(100, pages * Double(vm_kernel_page_size) / Double(ProcessInfo.processInfo.physicalMemory) * 100)
    }
}
