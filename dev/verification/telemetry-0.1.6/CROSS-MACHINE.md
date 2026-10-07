# 跨機對照驗證

狀態：此版本已做純邏輯、模擬電池與故障、原生 WPF 驗證；下列筆電／其他電腦實機測試尚未執行。

| 裝置 | 必測場景 |
| --- | --- |
| Intel 筆電核顯 | AC 滿電、充電中、電池放電、睡眠／喚醒 |
| AMD Ryzen 筆電核顯 | Tctl/Tdie/CCD 來源、電池、CPU 負載、睡醒 |
| NVIDIA 混合顯卡筆電 | CPU+GPU 負載、獨顯休眠／喚醒、0 W／缺值區分 |
| AMD 獨顯桌機 | Package/Core/SoC/PPT 重疊分項、全卡與晶片範圍 |
| 現有桌機 | CPU Package、GPU Core/Hot Spot/Memory、監測驅動與故障恢復 |

每台先以相同權限開啟 Momo 與 HWMonitor，等待 60 秒穩態，再對照同名感測器和相同範圍的讀值。CPU Package、Tctl/Tdie、CCD、GPU Core、Hot Spot、GPU chip、整卡功率不可互相替代。首頁估算總功耗不可當作 HWMonitor 的實測 CPU Package。

1. 記錄 Momo 版本、Windows 版本、CPU/GPU 型號、電源情境。請勿附主機名或序號。
2. 在 CPU/GPU 追查展開「感測詳情」，重置 HWMonitor min/max 後重新啟動 Momo，使比較期間一致。
3. 在 idle、CPU 負載、GPU 負載各取多個樣本；「匯出感測報告」保存來源 ID、單位、範圍、取樣時刻、min/max 和庫版本/hash。
4. 插拔電源，確認「充電功率」沒有加入整機功耗；電池放電時用電池端 W 對照。
5. 睡眠至少 10 分鐘後喚醒；能耗不能按醒來功率補算睡眠，第一次恢復採樣只建立基線。
6. 檢查缺值、relative battery units、更新失敗與 0 W。未提供讀數必須顯示缺值，不能回退成桌機額定瓦數。

可把溫度差 2–3°C、功耗差 `max(2 W, 5%)` 作為初始調查門檻；這是工程驗收起點，並非此版本的跨硬體精度承諾。不同取樣時刻、廠商 API 平滑及來源範圍會造成差異。整機牆插功耗仍需外部功率計。

已核對的官方資料：

- [HWMonitor](https://www.cpuid.com/softwares/hwmonitor.html)
- [LibreHardwareMonitor releases](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases)
- [Windows BATTERY_STATUS](https://learn.microsoft.com/en-us/windows/win32/power/battery-status-str)
- [QueryUnbiasedInterruptTime](https://learn.microsoft.com/en-us/windows/win32/api/realtimeapiset/nf-realtimeapiset-queryunbiasedinterrupttime)
- [NVIDIA NVML power telemetry](https://docs.nvidia.com/deploy/archive/R550/nvml-api/group__nvmlDeviceQueries.html)

底層已知限制：目前附帶庫為 0.9.6 自訂編譯，來源註明 upstream master，但缺少精確 commit。報告保留 DLL SHA256。RTX 50 Hot Spot 在此底層可能回報無效 0，不能靠顯示層補出值；Momo 會保持缺值。
