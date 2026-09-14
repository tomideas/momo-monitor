# StatusMonitor UI 重新設計 — Light Neo-Brutalism

日期：2026-09-12
狀態：實作中
取代：`2026-09-12-status-monitor-design.md` §9（UI 章節）；其餘章節（架構、資料流、build）仍有效。

## 1. 目標

將 WPF 系統狀態監控的視覺語言由「深色儀表」改為淺色新粗野風（light neo-brutalism），參考 Project Hub（kandao.1o3o.com）：

- 淺灰紙底、白卡片、2px 黑邊、硬偏移陰影、大寫英文 + 中文雙語標籤、等寬字體數字、少量高飽和強調色。
- **保留現有資訊架構與元件**（860×620 視窗、儀表 / 橫條 / DataGrid / 設定面板 / 資訊頁），不新增 widget，不改資料層。

## 2. 配色 token

| Key | Color | 用途 |
|-----|-------|------|
| `PaperBrush` | `#F3F3F3` | 視窗背景 |
| `CardBrush` | `#FFFFFF` | 卡片、選取 nav chip、icon 按鈕底 |
| `HairBrush` | `#E6E6E6` | 分隔線 |
| `TrackBrush` | `#E6E6E6` | gauge / metric 軌道 |
| `InkBrush` | `#111111` | 文字、2px 邊框、主線條 |
| `Ink2Brush` | `#4A4A4A` | 次級文字 |
| `MutedBrush` | `#8E8E8E` | 次要標籤 |
| `AccentBrush` | `#2563EB` | 主強調（gauge 藍弧、即時瓦數、metric fill、選取） |
| `AmberBrush` | `#F5B301` | 版本徽章 |
| `RedBrush` | `#EF4444` | 危險色 |

Resource key 名稱全部保留（舊程式碼 / XAML 以 key 引用），僅換 color 值。所有色集中定義於 `App.xaml`；`MainWindow.xaml` 不再重複定義 palette。

## 3. 字型

方案 B（完全自含）：static weights 複製至 `StatusMonitor\Fonts\` 並以 `<Resource Include>` 明確嵌入：

- Geist: Regular / Medium / SemiBold / Bold（`Geist-V.ttf` 不嵌入）
- Geist Mono: Regular / Medium / SemiBold / Bold（`GeistMono-V.ttf` 不嵌入）
- Noto Sans TC: Regular / Medium / Bold（CJK fallback）

字型鏈（WPF 以逗號 fallback）：

- UI：`pack://application:,,,/Fonts/#Geist, Noto Sans TC`（`AppFontFamily`）
- Mono：`pack://application:,,,/Fonts/#Geist Mono, Noto Sans TC`（`MonoFont`）

`AppFont.Resolve` 空值（內建）回傳 UI 鏈；Gauge / Metric 主要數字在 control 內使用 mono 鏈（static），label / sublabel 使用 `SetDefaultFont` 設定之使用者字型。

> 風險：pack URI 逗號 fallback 是否同時命中 embedded Geist 與 Noto Sans TC 未保證；以 `--render` 截圖驗證，若 CJK tofu 或拉丁非 Geist，備案改為以 Noto Sans TC 為基底。

## 4. 簽名元素

- **Card**：白底、`InkBrush` 2px 邊框、radius 10、padding 14、硬陰影 `DropShadowEffect BlurRadius=0 ShadowDepth=3 Direction=45 Color=#111111 Opacity=0.9`。
- **Nav chip**：未選取 = 透明底 / 透明邊框 / muted 文字；選取 = 白底 / 2px ink 邊框 / ink 文字 / SemiBold / `ShadowDepth=2` 硬陰影。
- **Icon 按鈕（⚙）**：白底、1.5px ink 邊框、radius 6。
- **版本徽章**：`AmberBrush` 底、1.5px ink 邊框、radius 6、mono `v0.1.0`。
- **警告 banner**：`#FEF3C7` 底、1.5px ink 邊框、radius 8、ink 文字。
- **Gauge**：270°，三層同粗 8px — hairline 軌道（E6E6E6，僅 90° 缺口可見）、ink 全弧（111111）、藍 value 弧（2563EB，round cap）；主數字 ink + mono，sublabel muted + 使用者字型。
- **MetricBar**：track 6px / radius 3 hairline，fill 藍 2563EB；label muted 使用者字型，value ink mono。
- **設定面板**：白底、2px ink、radius 12、`ShadowDepth=3` 硬陰影；Close = primary（黑底白字），Reset = secondary（白底 ink 邊框）。
- **頂欄**：`STATUS MONITOR · 系統狀態監控`（Bold 15）+ 版本徽章；nav 雙語 chip（DASHBOARD / INFO）。

## 5. 卡片標題（雙語，新增 Loc keys）

| Key | 值 |
|-----|-----|
| `app_brand` | STATUS MONITOR · 系統狀態監控 |
| `hdr_summary` | OVERVIEW · 總覽 |
| `hdr_cpu` | CPU · 處理器 |
| `hdr_gpu` | GPU · 顯卡 |
| `hdr_ram` | RAM · 記憶體 |
| `hdr_network` | NETWORK · 網路 |
| `hdr_storage` | STORAGE · 儲存 |
| `hdr_fans` | FANS & PUMP · 風扇與幫浦 |
| `hdr_top` | TOP PROCESSES · 熱門處理程序 |

`font_default` 更新為 `Built-in Geist + Noto Sans TC` / `內建 Geist + Noto Sans TC`。

## 6. 修改檔案

| 檔案 | 變更 |
|------|------|
| `StatusMonitor\Fonts\`（新增） | 11 個 static 字型 |
| `StatusMonitor.csproj` | Resource 改 explicit includes（原 glob 僅 `NotoSansTC-*.otf`，且目錄不存在） |
| `App.xaml` | 新 palette、`MonoFont` / `AppFontFamily` 改嵌入鏈 |
| `MainWindow.xaml` | 移除重複 palette；新 Card / Chip / Button styles；頂欄 brand+badge+chips；warning banner；卡片雙語標題（summary / fans 補標題）；設定面板新樣式 |
| `MainWindow.xaml.cs` | 修 `ApplyFont` CS0117（改 `SetDefaultFont`）；nav 改 chip state（`SetNav`） |
| `Views\GaugeControl.cs` | 新 brush、mono 主數字、ink 全弧 |
| `Views\MetricBar.cs` | 新 brush、mono value、barH 6 |
| `Settings\AppFont.cs` | default resolve 改 Geist+Noto 鏈 |
| `I18n\Loc.cs` | 新增 `app_brand` / `hdr_*`；更新 `font_default` |

## 7. 已知 pre-existing 問題（本次一併修復）

- baseline `dotnet build` 失敗：`MainWindow.xaml.cs(125,22)` / `(126,19)` CS0117（`GaugeControl` / `MetricBar` 無 static `FontFamily`）。現行 exe 為舊版 source 建置產物。
- `StatusMonitor\Fonts\` 不存在，原 glob 未嵌入任何字型，`pack://...#Noto Sans TC` 實際未生效。

## 8. 驗證

1. `build.ps1`（從 `tools\status` 執行）→ 產出 `StatusMonitor.exe`；另複製為 `StatusMonitor.new.exe`。
2. `--render` 三模式（dashboard / info / settings）截圖目視：淺色底、白卡黑邊、硬陰影、Geist 拉丁、Noto CJK、黃徽章、nav chip 選取態、gauge 三層弧、DataGrid。
3. `--dump` 回歸：sensor 數值與重設計前一致（資料層未改）。
4. 通過後更新 `2026-09-12-status-monitor-design.md` §9、`README.md`、`CHANGELOG.md`。

## 9. 應用程式 ICON + 頂欄 logo（momo capybara）

- 來源：`\\192.168.8.100\tomideas\temp\@coding\@momo\momo_pop.png`（754×754 pop-art 水豚：黃底藍點 + 墨黑粗框）；頂欄「logo + 品牌字」作法參考 kandao.1o3o.com Project Hub。
- 來源已有透明背景（四角 flood fill 移除 0px），但邊緣帶白色 halo：邊緣亮像素（lum > 150）alpha 柔化（2107px），深色底乾淨。
- `StatusMonitor\app.ico`（自行組裝：256 為 PNG entry，64/48/32/16 為 BMP entries）：csproj `<ApplicationIcon>app.ico</ApplicationIcon>`（exe / 工作列）+ `<Resource Include="app.ico" />`；`MainWindow Icon="pack://application:,,,/app.ico"`（標題列）。
- `StatusMonitor\Assets\momo_pop.png`（去 halo 版，`<Resource Include>`）：頂欄 brand 左側 28px `Image`（`Source="pack://application:,,,/Assets/momo_pop.png"`，HighQuality scaling）。
- 驗證：正式 `build.ps1` → `StatusMonitor.exe`（23.6MB）；`ExtractAssociatedIcon` 取出 capybara；`StatusMonitor.g.resources` 含 `app.ico` + `assets/momo_pop.png`（與 XAML 參照一致）；`--render` 建構成功；深色底預覽邊緣乾淨。
