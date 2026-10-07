# Momo 系統監測 — 風扇使用者命名與溫度來源

日期：2026-09-24
狀態：已實作（`FanFollow.Category`，v0.1.3）
範圍：Fans 頁的風扇辨識。採 Fan Control 式的「使用者指定」，不引入內建板卡對應表。

## 1. 為什麼

主機板 SuperIO 常常不給有意義的風扇名稱。實測機（ASUS ROG STRIX Z790-I GAMING WIFI，
Nuvoton NCT6798D）經 `--diag` 取得的名稱是 `Fan #1`…`Fan #7`，沒有任何 `CPU Fan`/
`Chassis Fan`/`Pump` 字樣。

目前 `FanControlService.Discover` 只用感測器名稱推導兩個欄位：

- `Kind`（`FanControlService.cs:151`）：gpu / cpu / fan，決定這個風扇跟隨哪個溫度。
- `Icon`（`FanControlService.cs:152`）：列上的圖示。

名稱是通用的，於是 `Kind` 一律落到 `"fan"`（跟 max(CPU, GPU)）、圖示一律是普通風扇。
使用者無法辨識哪顆是 CPU、哪顆是機殼，也無法讓某顆風扇只跟隨 CPU 或 GPU。

這不是「讀不到」——感測器都正常；缺的是**使用者可指定的辨識**。

### 參考：Fan Control 的做法

Fan Control（Rem0o）對這個問題的答案是：**不猜**。它把 control（風扇）與 sensor（溫度）
分成兩份清單照原樣顯示，讓使用者自己配對轉速感測器、自己在校正後指定 fan curve 的
Temperature source。沒有內建 CPU/機殼對應表。本設計照此原則，只做「使用者指定」。
（見 https://getfancontrol.com/docs/ 的 Control 與 fan curve 章節。）

## 2. 目標

- 使用者在 Fans 頁可為每個可控風扇**改名**（行內編輯，隨時可用，不需進入 Custom）。
- 使用者可指定每個風扇**跟隨哪個溫度來源**：自動 / CPU / GPU / 較高者。
- 未命名的機器，行為與現況**完全相同**（向後相容，零回歸）。
- 不需要社群、不需要維護硬體資料庫。

## 3. 非目標（本波不做）

- 唯讀風扇列出（有轉速、無 `IControl` 的風扇）。
- 0 RPM（GPU 停轉）與未提權 / 未裝驅動的狀態顯示與引導。
- 上游 LibreHardwareMonitor 的可重複重建流程。
- 內建板卡對應表。
- 外掛架構。

以上各自獨立，可另立規格；本規格只交付 Fans 頁的命名與來源指定。

## 4. 設計

### 4.1 資料模型與持久化

新增 `AppSettings.FanIdentities`：

```csharp
public enum FanSource { Auto, Cpu, Gpu, Max }

public sealed class FanIdentity
{
    /// <summary>空字串＝使用硬體回報的原始名稱。</summary>
    public string Name { get; set; } = "";
    public FanSource Source { get; set; } = FanSource.Auto;
}

// AppSettings
public Dictionary<string, FanIdentity> FanIdentities { get; set; } = new();
```

- key = 控制感測器的 `Identifier.ToString()`（例 `/lpc/nct6798d/0/control/0`），
  與 `FanProfile.ControlId` 同一把鑰匙，跨重啟穩定。硬體變動只會留下孤兒條目，
  被忽略而非套用到錯的風扇。
- 存法比照既有的 `GpuTdpWatts`（`AppSettings.cs`）。

**為何獨立於 `FanProfile`**：命名/來源是「這個風扇是什麼」，曲線是「怎麼驅動它」。
改名要在 `Auto` 模式下也能用、要立即生效，不該被 Custom 面板的 draft/Apply 綁住
（`FanProfileVm._draft`）。`FanProfile` 維持只管曲線。命名/來源變更立即寫入並 `Save()`。

### 4.2 服務行為

`FanControlService.ReadSource`（目前 `FanControlService.cs:217`）以 `target.Kind` 決定
來源。改為：先看 `FanIdentities[target.Id].Source`，

- `Auto` → 沿用 `Kind`（gpu 跟自家卡、cpu 跟套件、其他跟 max）。
- `Cpu` / `Gpu` / `Max` → 使用者指定；缺該類別時安全退回另一個（與現行 fallback 相同）。

`Apply(hub, settings)` 已接收 `AppSettings`（目前用它查 `FanProfiles`）；同一處再查
`FanIdentities` 即可。`Kind` 欄位保留，作為 `Auto` 的預設。其餘（clamp、驅動失敗退回、
`SourceTemperatureC` 顯示）不變。

### 4.3 UI（Fans 頁）

- 每列標題（`MainWindow.xaml` 目前綁 `Title`）改為**行內可編輯**：點擊進入編輯，
  失焦或 Enter 提交，Esc 取消。提交後寫 `FanIdentities[id].Name` 並 `Save()`；
  清空 = 回復顯示原始感測器名。
- Custom 面板（`CustomPanel`）新增一排來源 chips：
  `跟隨：自動 / CPU / GPU / 較高者`，選了立即生效並存檔（比照 `Auto` radio 的即時語意）。
- 其餘列結構與 Auto/Custom 曲線邏輯不動。

### 4.4 圖示

`Target.Icon` 目前由服務建立、只被 Fans 頁 DataTrigger 消費（`MainWindow.xaml:545-566`）。
改為在 `FanProfileVm` 上算 `Icon`：以**有效名稱**（`Name` 非空就用它，否則 `SensorName`）
呼叫既有的 `FanNaming.IconFor`。XAML DataTrigger 由 `Target.Icon` 改綁 VM 的 `Icon`。

如此命名成 "CPU Fan" 就會顯示 CPU 風扇圖示；`FanTarget` 保持單純的硬體模型。

### 4.5 顯示文字

`FanProfileVm.Title` 改為：`Name` 非空 → `Name`；否則沿用現行（`SensorName` 為空則
`HardwareName`）。`HardwareText`（tooltip）仍顯示原始硬體 + 感測器名，讓使用者知道底層事實。

### 4.6 i18n

`Loc.cs` 新增（en / zh-Hant）：

- `fan_follow` — Follow / 跟隨
- `fan_follow_auto` — Auto / 自動
- `fan_follow_cpu` — CPU / CPU
- `fan_follow_gpu` — GPU / GPU
- `fan_follow_max` — Hotter of CPU & GPU / 較高者
- `fan_rename_hint` — Rename / 重新命名

## 5. 相容性

- 舊 `settings.json` 無 `FanIdentities` → 空 dict、每個風扇 `Auto`／未命名 → 行為與現況一致。
- 新增欄位有預設值，序列化往返不影響舊欄位。
- 孤兒條目（硬體移除）被忽略，不套用到其他風扇。

## 6. 測試（`dev/tests/FeatureChecks/Program.cs`）

- **來源解析純函式**：抽出「`FanSource` → 實際類別」的判斷，對
  {Auto, Cpu, Gpu, Max} × {有/無 CPU 來源, 有/無 GPU 來源} 全組合斷言；
  `Auto` 的結果必須與現行 `Kind` 邏輯一致（`Kind == "gpu"` 的風扇 `Auto` 仍跟 GPU）。
- **設定往返**：`FanIdentities` 序列化 → 反序列化保真。
- **舊檔相容**：無 `FanIdentities` 的 JSON → 空 dict、不拋錯。
- 保留既有：`FanNaming` 命名、`kind != gpu || HasGpuSource`（`MainWindow.FeatureChecks.cs:230`）等。

## 7. 風險與限制

- 儀表板的「CPU 風扇」數字走另一條路（`CpuSensor.ReadCpuFan`，靠名稱含 "CPU"，
  找不到時退回「轉最快的風扇」），此波不動 → 通用命名板子的儀表板仍可能挑到非 CPU 扇。
  若要一致，需另立規格。
- 使用者若把機殼扇標成 CPU 並設 `Source = Cpu`，該扇將只跟隨 CPU，GPU 滿載時不加速。
  這是使用者的明確選擇（與 Fan Control 相同語意）；UI 文案需讓「跟隨」的後果可理解。

## 8. 交付時同步

依專案 `AGENTS.md` 第 7 條：更新 `CHANGELOG.md` 頂部與 `StatusMonitor.csproj` 版本
（目前 `0.1.2`）。涉及 UI 的共用設計若有變動，依 `AGENTS.md` UI 規範回寫設計系統。
