# StatusMonitor — 設計文件

> 最後更新：2026-09-12
> 狀態：**已實作並建置**（`StatusMonitor.exe`）
> 位置：`itx/tools/status/`

## 1. 目標

Windows 11 桌面小工具（WPF, .NET 8），單一視窗即時顯示整機狀態，視覺參考
開源專案 WattSeal（Rust，GPLv3）。該執行檔原本放在 `reference/`，公式移植完成後已移除。

- 每 1 秒更新
- 以管理員身分執行（`app.manifest` → `requireAdministrator`；讀取硬體感測器需要）
- 介面語言可切換：English / 繁體中文

## 2. 範圍

### 顯示項目
| 卡片 | 內容 |
|------|------|
| CPU | 負載圓環、Temperature / Clock / Fan / Power 條 |
| GPU | 負載圓環、Temperature / Clock / Fan / Power 條 |
| RAM | 負載圓環、已用/總量 |
| Network | 下載 / 上傳速度 |
| Storage | 負載圓環 |
| 風扇與幫浦 | 主機板所有風扇／水冷幫浦轉速（SuperIO） |
| 總覽 | 目前瓦數 (W)、累計用電 (kWh/Wh)、碳足跡 (g/kg CO₂)、電費 (幣別) |
| Top Processes | CPU%、GPU%、RAM、瓦數、磁碟 I/O（依瓦數排序） |

**信息頁**（與儀表板切換，WMI／登錄／API）：CPU（型號、核心/執行緒）、GPU（內顯＋獨顯）、記憶體（總量、Swap）、系統（OS 版本＋組建、主機名）、儲存（各磁碟可用/總量）、電池、顯示器（型號、模式）。

### 設定
- 語言：English / 繁體中文
- **國家／地區（單一選單）**：選一次同時套用電價、幣別與碳強度；預設 World average。
  選項：France / Germany / UK / USA (average) / China / India / Sweden / Poland / World average / Custom
- 電價（＋幣別 42 種）與碳強度可再手動微調；一旦手動改值，國家自動變為 Custom
- 顯示程序數、重置累計用電

### 不做（v1）
歷史圖表、系統匣常駐、每程式網路速度、多主題、其他語言、其他平台。

## 3. 技術架構

單一 WPF (.NET 8) 程序。因為專案位於網路路徑，`build.ps1` 會複製到本機暫存後
以 `dotnet publish -p:PublishSingleFile=true` 產出單一 exe。

```
tools/status/
  StatusMonitor/
    StatusMonitor.csproj / NuGet.Config / app.manifest (+ app.asinvoker.manifest 測試用)
    App.xaml(.cs)            進入點；--dump / --diag / --render 測試模式
    MainWindow.xaml(.cs)     儀表板 + 設定面板
    Views/                   GaugeControl（自繪圓環）、MetricBar（自繪長條）
    ViewModels/              MainViewModel（每秒輪詢、格式化）
    Models/Snapshot.cs       樣本資料
    Sensors/                 HardwareMonitorHub、CpuSensor、CpuClockSensor、
                             GpuSensor、RamSensor、NetworkSensor、
                             StorageSensor、ProcessSensor
    Power/PowerModel.cs      瓦數與每程式分攤（移植 WattSeal 公式）
    Settings/AppSettings.cs  設定 + 預設清單 + JSON 存取
    Services/MonitorService.cs 聚合所有感測器
    I18n/Loc.cs              繁中/英文字串
  build.ps1
  dev/  （docs 與 tests）
```

## 4. 感測來源與公式

| 元件 | 來源 | 備註 |
|------|------|------|
| CPU 負載 | LibreHardwareMonitor（`CPU Total`） | |
| CPU 溫度/瓦數/時脈 | LibreHardwareMonitor（**自行建置 master ＋ PawnIO 驅動**）：Tctl/Tdie、Package Power、各核心時脈 | 需 PawnIO |
| 風扇／水冷幫浦 | LibreHardwareMonitor（主機板 **SuperIO**，本機為 Nuvoton NCT6701D） | 依主板型號支援度 |
| GPU | `nvidia-smi`（負載/溫度/時脈/風扇/瓦數） | 風扇為**百分比** |
| RAM | `GlobalMemoryStatusEx` | |
| 儲存 | `PhysicalDisk` 效能計數器（`% Disk Time`、讀寫速率） | |
| 網路 | `NetworkInterface` 統計差分 | |
| 每程式 CPU/RAM | `Process.TotalProcessorTime`／`WorkingSet64` 差分與快照 | |
| 每程式 GPU | `GPU Engine(*)\Utilization Percentage`（快取 `PerformanceCounter` + `NextValue()`） | |
| 每程式磁碟 | `GetProcessIoCounters` 差分 | |

### 耗電公式（移植 WattSeal）
- CPU：RAPL/套件瓦數；不可得時 `TDP_idle + (TDP_peak−TDP_idle)·usage^1.6`（idle=20%、peak=125% TDP）
- GPU：`nvidia-smi` 直讀
- RAM：固定 5 W
- 儲存：SSD `0.05 + MB/s·0.015`（HDD `3.0 + MB/s·0.035`）
- 網路：`0.2 + MB/s·0.01`（每介面上限 3 W）

### 每程式分攤
```
procCpuEnergy = (procCpu% / totalCpu%) × cpuWatts
procGpuEnergy = (procGpu% / totalGpu%) × gpuWatts
procWatts     = ((procCpuEnergy + procGpuEnergy) / (cpuWatts + gpuWatts)) × totalWatts
```
依 app 名稱分組；每程式 CPU% = 程序 CPU 時間 ÷ 核心數。

### 累計與費用
```
累計用電 kWh = ∫ 瓦數 dt / 1000         （持久化 totals.json，可重置）
碳足跡 g CO₂ = 累計 kWh × 碳強度 g/kWh
電費         = 累計 kWh × 電價 /kWh
```

## 5. 持久化
`%APPDATA%\StatusMonitor\`
- `settings.json`：語言、電價、幣別、碳強度、程序數
- `totals.json`：累計用電 Wh

## 6. 驗證模式（測試鉤子）
| 參數 | 用途 |
|------|------|
| `--dump --out <file>` | 取樣一次輸出 JSON（核對數值） |
| `--diag --out <file>` | 列出 LibreHardwareMonitor 可見的所有感測器（除錯） |
| `--render <file.png>` | 把整個儀表板算繪成 PNG（版面驗證，不受螢幕限制） |

## 7. 授權
WattSeal 為 GPLv3。本工具以 C# **重新實作公式與版面**（非複製其原始碼）；自用無虞，對外散布需自行評估授權義務。

## 8. 驗收結果
- 建置成功（.NET 8／9 SDK；自行建置 LibreHardwareMonitor master ＋ PawnIO 2.2.0），產出單一 `StatusMonitor.exe`（約 5 MB，framework-dependent，需 .NET 8 Desktop Runtime）。
- 排版與參考圖一致：五張卡 ＋ 風扇與幫浦 ＋ 總覽 ＋ Top Processes；圓環/長條自繪。
- 中/英介面切換正常（已渲染比對）。
- GPU 數值與 `nvidia-smi` 一致；每程式 GPU/瓦數合理（提權後 vmwp 32%、rustdesk 20% 等）。
- CPU 溫度（Tctl/Tdie）、套件瓦數與各核心時脈可取得；主機板風扇／水冷幫浦可取得（本機：AIO Pump 3367、CPU Fan 1172、Chassis Fan 1220 RPM）。
- 非管理員或未裝 PawnIO 時顯示提示，溫度/風扇/CPU 瓦數降級；其餘功能正常。

## 9. UI 風格（Light Neo-Brutalism）

> 完整規格見 [`2026-09-12-status-monitor-ui-redesign.md`](./2026-09-12-status-monitor-ui-redesign.md)。

- **方向**：淺色新粗野風（light neo-brutalism），參考 Project Hub（kandao.1o3o.com）。淺灰紙底、白卡片、2px 墨黑邊框、硬偏移陰影、大寫英文 + 中文雙語標籤、等寬數字、少量高飽和強調色。
- **配色**（集中於 `App.xaml`）：paper `#F3F3F3`、card `#FFFFFF`、hairline/track `#E6E6E6`、ink `#111111`、ink2 `#4A4A4A`、muted `#8E8E8E`、accent blue `#2563EB`、amber `#F5B301`、red `#EF4444`。
- **字型**：方案 B — 自含。`StatusMonitor\Fonts\` 內嵌 Geist / Geist Mono / Noto Sans TC 的 static 權重（11 檔，csproj 採 explicit `<Resource Include>`，不含 variable 字型）。`AppFontFamily`：`pack://application:,,,/Fonts/#Geist, Noto Sans TC`；`MonoFont`：`pack://application:,,,/Fonts/#Geist Mono, Noto Sans TC`。
- **量體**：視窗 **860×620**（可縮放，最小 720×480）；卡片 padding 14、圓環 96（下排 84，thickness 8、ink 全 270° 弧 + 藍 value 弧）、度量長條 22px（barH 6、radius 3、藍 fill）、列距 5；Top Processes 表格內部捲動，Cell style 選取底色 `#1A2563EB`。
- **簽名元素**：白卡 + 2px 墨黑邊 + 右下 hard shadow（`DropShadowEffect BlurRadius=0 ShadowDepth=3 Direction=45 Opacity=0.9`）；nav chip 選取態為白底 + 2px ink 邊 + SemiBold + `ShadowDepth=2`；版本徽章 `v0.1.0` 為黃底墨邊；警告 banner 為 `#FEF3C7` 底；Gauge 主數字固定使用 embedded Geist Mono，sublabel 跟隨 UI 字型。

## 10. 已知限制

- **風扇／CPU 溫度需 PawnIO 驅動**：新版 LibreHardwareMonitor 改用 **PawnIO**（簽章驅動，非被封鎖的 WinRing0）做低階存取。首次使用需一次性安裝：`winget install namazso.PawnIO -e`。未安裝時 CPU 溫度/風扇/板載電壓顯示 `—`。
- **主機板風扇／SuperIO 支援依板子型號**：本機 ASUS ROG STRIX B850-I 為 Nuvoton NCT6701D，已於 LHM 上游 PR #1704 支援（須自行建置 master；NuGet 尚無）。不支援的板子顯示 `—`。
- **GPU 風扇**：`nvidia-smi` 的 `fan.speed` 是**百分比**（非 RPM），已以 `%` 顯示。
- 硬體文件 `01-system.md` 記載為 Intel i7-14700K / Z790-I，與本機實測（9900X / B850-I）不符，待確認。
- 單檔 exe 為 framework-dependent，執行端需 .NET 8 Desktop Runtime。
- `PhysicalDisk % Disk Time` 在 NVMe 上偶爾會瞬時接近 100%。
