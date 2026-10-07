# 0.1.6 驗證記錄

2026-10-07：溫度、功耗、電池、能耗覆蓋與報告功能已實作。

- 主專案 Release 構建成功，0 compiler warnings / 0 errors。
- 七組純邏輯／mock 檢查共 696 項：FeatureChecks 443、AlertChecks 44、FanChecks 43、HubChecks 18、PowerChecks 66、TelemetryChecks 40、BatteryChecks 42。
- PAPER POP / VOLT 原生 WPF 152 項既有互動檢查通過；供電情境各 9 項新增檢查驗證電池方向、功耗範圍、來源、min/max、報告入口與 preview 隔離。
- 代表畫面包含繁中／English、460×760、460×330、1040×760；以 PNG 目視檢查充電功率與整機功耗分開、感測詳情和窄視窗捲動。
- Premium static audit 在產品來源 StatusMonitor 範圍為 0 findings；該掃描器不完整解析 WPF，原生畫面和互動檢查才是主要 UI 證據。
- 根執行檔為 0.1.6，自包含 Windows x64。感測庫的 DLL SHA256 與內嵌 provenance JSON 一致。
- 依最新使用者指示，build.ps1 預設僅更新執行檔；`-Portable` 是明確要求打包時的選項。預設流程驗證發佈 ZIP 時間與內容不變。

預設建置流程重新驗證時，restore/publish 完成，但根執行檔正在使用而無法替換；腳本正確回報並保留程式。既有根執行檔已為本次 0.1.6，沒有關閉使用者程式或建立新的 ZIP。

限制：尚未在 Intel／AMD／混合顯卡筆電或多部其他電腦做 HWMonitor 精度對照。電池與故障場景使用 mock，不代表所有 OEM 支援。無真實風扇寫入或驅動安裝。

自包含建置的 NuGet vulnerability-data 查詢受到網路限制，產生 NU1900；已快取套件仍完成 restore／publish。這不是感測精度驗證。

可刪檔案檢查：根 MomoMonitor.Test.exe 為 0.1.6 免 UAC 測試版；MomoMonitor_DESKTOP-Q5RVSA7_Oct-07-002729-2026_Conflict.exe 為 0.1.5 舊執行檔，沒有產品來源依賴。兩者皆不是設定／歷史，尚未執行刪除；momo-data 需保留。

跨機流程見 [溫度與功耗比較規範](../../docs/temperature-power-validation.md)。
