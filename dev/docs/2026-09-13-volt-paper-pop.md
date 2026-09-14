# Momo 系統監測 UI 改版 — VOLT / PAPER POP

日期：2026-09-13
狀態：已實作
取代：`2026-09-12-status-monitor-ui-redesign.md`（light neo-brutalism）。架構、資料流、build 章節仍以 `2026-09-12-status-monitor-design.md` 為準。

## 1. 為什麼改

前一版把運動 App 的**顏色**搬了過來，沒搬它的**結構**。參考畫面之所以成立，是因為一頁只有一個英雄數字，滿版飽和色是用來襯托那個唯一焦點。舊儀表板有九張等重的卡、二十幾個數字，飽和底色沒有焦點可襯，只剩背景噪音。

同時 `Theme.cs` 在七個淺色主題下把 `AccentBrush` 與 `AmberBrush` 都設成墨黑 `#101513`，等於整頁只有「底色 ＋ 黑」兩個值。五個圓環並排長得一模一樣，13% 與 27% 的弧長差在細環上讀不出來。

## 2. 三條規則

| # | 規則 | 推論 |
|---|------|------|
| 1 | 一頁一個英雄 | 英雄數字給**目前功耗**，不給 CPU%。用電／碳／電費是這支工具獨有的，CPU 百分比每個監控軟體都有。累計用電、碳足跡、電費降為英雄旁的三小格。 |
| 2 | 顏色只說一件事 | 所有粗條一律用強調色；只有超標（負載 ≥ 90%，磁碟則是剩餘空間低於門檻）才轉警示色。見下方「§9 顏色規則的修正」。 |
| 3 | 粗條取代圓環 | 五項負載共用同一條基準線，長度直接可比。`GaugeControl` 已移除。 |

## 3. 兩套皮

`Theme.Keys` 由七個縮為 `volt` / `paper`。`Theme.Normalize` 把七個舊 key 映射過去（`night` → `volt`，其餘 → `paper`），舊設定檔仍可讀。

| Token | VOLT | PAPER POP | 用途 |
|-------|------|-----------|------|
| ground | `#0B0B0B` | `#EDEAE1` | 視窗底（`PaperBrush`） |
| raise | `#141413` | `#FFFFFF` | 凸起區塊、輸入框（`CardBrush`） |
| line | `#262624` | `#D5D1C5` | 分隔線、條軌（`HairBrush` / `TrackBrush`） |
| ink | `#F4F3EF` | `#0D0D0C` | 主文字、一般粗條（`InkBrush`） |
| muted | `#9A978C` | `#545149` | 次要文字、標籤（`MutedBrush`） |
| pop | `#D4FF00` | `#0A3CFF` | 所有粗條、英雄數字、圖示重點（`AccentBrush` / `AmberBrush`） |
| alarm | `#FF3B14` | `#FF2E00` | 超過門檻時取代 pop（`AlarmBrush`，新增） |

### 次要文字的對比度（量過，不是憑感覺）

初版 muted 是 `#77746A`（Volt）／`#6E6B60`（Paper Pop），實測對比度只有 **4.21** 與 **4.44**，
都在 WCAG AA 小字門檻 **4.5** 之下 —— 而這些標籤是 10px 等寬字，最吃虧的尺寸。深色皮上尤其難讀。

改為 `#9A978C`／`#545149`，實測 **6.73** 與 **6.59**。兩套皮的可讀性刻意拉齊，且距離 ink 的
17.73 仍然很遠，所以「主／次」的層級沒有被破壞 —— 能讀，但不搶。

驗證方式不是看截圖，而是**從算繪出的 PNG 逐像素掃出字芯顏色**再套 WCAG 相對亮度公式計算，
確認畫面上真正畫出來的值符合預期，而非只有 token 設定正確。

另移除 `Ink2Brush`：全專案只有 `Theme.cs` 設定它、沒有任何消費者，是死 token。

Resource key 名稱全部保留，只換語意與值。`PopPaper` 恢復為每套皮各自的 20 DIP 點陣 `DrawingBrush`（舊版被 `Set("PopPaper", background)` 用純色覆蓋，README 宣稱的網點其實看不到）。

## 4. 字體

| 角色 | 字體 | 說明 |
|------|------|------|
| 英雄／百分比 | **Barlow Condensed** Black Italic | 新增嵌入。heavy ＋ condensed ＋ **原生真斜體**（非 SkewX 假斜）。已驗證含 `tnum`，配 `Typography.NumeralAlignment="Tabular"` 鎖住位寬，每秒跳動不位移。 |
| UI／標籤 | Geist（沿用） | Google Fonts 的 Archivo 目前只有 variable 版，WPF 對 variable font 支援不佳，故不採用。 |
| 即時小數值 | Geist Mono（沿用） | 次要數值維持等寬。 |

字級階梯：`MicroLabel` 9 → `RowSub` 10 → `RowValue` 34 → `HeroValue` 92。舊版最大落差只有 2.3 倍。

## 5. 一併修掉的既有問題

| 位置 | 症狀 |
|------|------|
| `Theme.cs` | 七個淺色主題的 accent 全是墨黑，等於沒有強調色 |
| `MainWindow.xaml` 信息卡 | 標題 chip 底色用 `AmberBrush`（＝墨黑）配白字，渲染成一排「黑色遮蔽條」 |
| `Theme.cs` | `PopPaper` 點陣被純色覆蓋 |
| `App.xaml` | `TextBox` / `ComboBox` / `TabItem` 顏色寫死，深色皮下出現白底控制項。三者改為 token 化並自帶 `ControlTemplate`（stock Aero 模板會自繪淺色 chrome 並忽略 `Background`） |
| `App.xaml` | 自訂 ComboBox 模板下 `DisplayMemberPath` 不會流進 `SelectionBoxItemTemplate`，選取值顯示成 `{ Id = volt, Name = ... }`。改用明確的 `ItemTemplate`（`NameItemT`） |
| `MainWindow.Features.cs` | `SetCompactMode` 會把 `DashboardCards.Columns` 重新綁回寬度轉換器；切過精簡再切回來，儀表板會散成多欄網格、破壞共用基準線。已改為固定單欄 |

## 6. 修改檔案

| 檔案 | 變更 |
|------|------|
| `Views/Theme.cs` | 重寫為兩套皮＋語意 token；`Normalize` 舊 key 映射；`DotPaper` 還原網點 |
| `Views/MetricBar.cs` | 新增 `BarTone` enum、`Tone` / `BarHeight` / `ShowText` 屬性；粗於 6 DIP 時改方角 |
| `Views/GaugeControl.cs` | **刪除** |
| `Converters/ToneBrushConverter.cs` | 新增。`BarTone` → ink／accent／alarm |
| `ViewModels/MainViewModel.cs` | 色調判定（見 §9）；`CpuTone` / `RamTone` / `DiskRows`；`GpuCard.Tone` / `LoadFraction` / `SubText`；`TotalWattsValue`、`CpuSubText`、`RamSubText`、`StorageSubText`、`StorageKindText` |
| `ViewModels/MainViewModel.Features.cs` | 新增 `CpuName` |
| `MainWindow.xaml` | 英雄區取代總覽卡；卡片網格改單欄粗條列表；新增型錄樣式（`MicroLabel` / `HeroValue` / `RowValue` 等）；三個資料繫結 ComboBox 改 `ItemTemplate` |
| `App.xaml` | `DisplayFont` 改 Barlow Condensed；palette 預設值；字級 token；輸入控制項模板 |
| `MainWindow.Features.cs` | `SetCompactMode` 固定單欄 |
| `MainWindow.FeatureChecks.cs` | 主題對比斷言改對 `volt`；新增「accent 不得等於 ink」；色調斷言見 §9 |
| `I18n/Loc.cs` | `theme_volt` / `theme_paper` / `available` |
| `StatusMonitor.csproj` | 嵌入三個 Barlow Condensed 字重 |

## 7. 第二輪調整（同日）

| 項目 | 做法 |
|------|------|
| 中文名稱 | 「豚豚監控」改為「**Momo監控**」（`app_title` / `app_brand`）。英文名維持 Momo Monitor。 |
| 迷你視窗無處可拖 | 拖曳原本只掛在標題列那條細帶上，且沒有任何視覺提示。改為**整個面板可拖**（`DragMini` 掛在外層 `Border`），並在頂部加 40×4 的握把；`OnControl` 沿視覺樹往上找 `ButtonBase`，所以還原鈕與置頂勾選框仍正常點擊。高度 238 → 252。 |
| 列標籤太小 | 每列加 24 DIP 圖示（CPU 晶片／GPU 板卡／RAM 記憶體條／DISK 磁碟／NET 上下箭頭），標籤字級 10 → 12；標籤欄寬 46 → 74。 |

### 圖示：兩色、24 單位網格

依使用者提供的參考圖，改為**粗圓筆畫的 ink 輪廓 ＋ 一個 accent 色重點元素**（CPU 中心點、GPU 圓環、
RAM 內部直條、DISK 中間弧線、NET 向下箭頭）。純 `Geometry` 資源，不依賴圖示字型，兩套皮自動換色。

兩個實作上的坑：

- **網格必須是 24 單位，不能是 16。** 在 16 單位下 2.0 的筆畫佔掉 12.5%，CPU 接腳間距只剩 0.3 單位，
  渲染後糊成一團。改 24 單位後筆畫佔 8.3%，間距 1.0 單位，細節才分得開。
- **`Stretch` 必須是 `None`。** `Uniform` 會把**每個 `Path` 各自**正規化到它自己的邊界，於是那顆小小的
  中心點被放大到填滿整個方框。兩層要共用同一個 24 單位座標系，靠外層固定尺寸的 `Grid` 對齊。

**圖示重點元素一律使用 accent 色**：Paper Pop 為藍、Volt 為螢光綠，整欄讀起來是同一套。

中間曾試過「重點元素平時為灰、只有最高佔用那列轉 accent」，但實際渲染下灰色（`#6E6B60`）在米白底上
太接近輪廓色，兩色結構整個消失，整欄只剩一顆有顏色 —— 看起來像壞掉而不是有意的層次。已改回統一上色。

圖示重點很小（一個點、一個小環），主要層級仍由字級與粗條長度建立。
（`ConverterParameter=muted` 分支已於 §9 移除。）

迷你視窗的數值也一併改為 Barlow Condensed Black Italic ＋ tabular，與儀表板一致（原本指定 `FontWeight="Bold"` 但該字重未嵌入，會由系統合成）。

### 標題列上色（參考 NZXT CAM）

標題列原本是系統預設的淺色 chrome，在 Volt 皮上等於頂著一條白帶。`Views/TitleBar.cs` 以 DWM 屬性
（`DWMWA_CAPTION_COLOR` 35、`DWMWA_TEXT_COLOR` 36、`DWMWA_BORDER_COLOR` 34、`DWMWA_USE_IMMERSIVE_DARK_MODE` 20）
把**原生**標題列染成 `ground`／`ink`／`line`。

選染色而不是 `WindowStyle="None"` 自繪 chrome，是因為自繪要重做最小化／最大化／關閉／拖曳／縮放邊框，
還會失去 Snap Layouts 與正確的 DPI 行為 —— 為了一條色帶付這個代價不划算。

- DWM 吃的是 `0x00BBGGRR`，位元組順序與一般 RGB 相反。
- HWND 要等視窗顯示後才存在，所以 `Theme.Apply` 之外還要從 `SourceInitialized` 再呼叫一次 `ApplyCaption`。
- 需 Windows 11（build 22000+）。更舊的版本上 DWM 呼叫會回傳失敗 HRESULT，標題列維持系統預設，不影響其他功能。
- 驗證方式：`--render` 只截視窗內容、拍不到標題列，因此改用螢幕實拍再取樣像素。實測 Volt `#0B0B0B`、Paper Pop `#EDEAE1`，與 token 完全吻合。

## 8. 信息頁：儲存與顯示記憶體（參考 NZXT CAM）

### 修掉的三件事

| 問題 | 說明 |
|------|------|
| **儀表板完全看不到硬碟容量** | DISK 列的粗條與百分比讀的是 `% Disk Time`（I/O 忙碌率），不是空間佔用。`0%` 的意思是「現在沒在讀寫」，不是「硬碟是空的」。副標原本是讀寫速率，現在改為各磁碟的已用／總量。 |
| **信息頁的容量寫反了** | 原本是 `{free} / {total}` 但標籤只寫「空間」，`337.53 GB / 476.72 GB` 會被讀成「用了 337」，實際是「剩下 337」。改為「已用 X / Y（Z%）」＋ 橫條。 |
| **每顆硬碟各自一張卡** | 兩顆硬碟看起來像兩個無關的元件。改為一張「儲存」卡、每顆硬碟一列，沿用 CAM 的版面。 |

### DISK 列：每顆硬碟各自一格，左右並排

參考 NZXT CAM 的 Storage 面板：多顆硬碟在**同一列內左右均分**，不新增卡片也不新增列。
每格是「磁碟代號 ＋ 橫條 ＋ 百分比」，下方一行已用／總量。以 `UniformGrid Rows="1"` 均分寬度，
所以一顆硬碟時佔滿整列，三顆以上仍會自動縮放。

**沒有總計百分比是刻意的**：把快滿的 SSD 和空曠的 HDD 平均起來，剛好會蓋掉那顆需要注意的。

`DiskRowVm` 採**就地更新**而非每秒重建集合 —— 若每次 `OnPropertyChanged(null)` 都換掉項目，
`MetricBar` 的過場動畫會每秒從 0 重跑一次，視覺上會看到規律脈動。只有在磁碟數量或代號改變時才重建。

隨之移除的死碼：`StorageLoad`／`StorageLoadText`／`StorageFraction`／`StorageTone`／
`StorageKindText`／`StorageSubText`（原本的總計版本，改成分列後不再有人引用）。

### DISK 列＝佔用率

DISK 的粗條與百分比是**空間佔用率**，不是 `% Disk Time`。先前的版本用 I/O 忙碌率，
理由是「容量幾乎不動，滿的硬碟會永遠霸佔強調色」—— 但那個顧慮隨著 §9 的顏色規則修正一起消失了：
現在顏色不代表「最高」，所以佔用率高不會搶走任何東西。

`% Disk Time` 感測器保留但不再顯示於任何畫面；功耗模型用的是讀寫位元組（`DiskReadBytesPerSec` /
`DiskWriteBytesPerSec`），不受影響。

### 實作

- `Snapshot.DiskVolume`：既有的 30 秒磁碟掃描原本只留剩餘百分比、丟掉絕對 GB，現在同一個迴圈一併保留
  已用／總量／磁碟區標籤，**不增加任何 I/O**。`DiskFreePercent` 改為從它衍生，維持提醒引擎的介面不變。
- `InfoItem.Fraction`（`double?`）：有值時信息卡多畫一條橫條，沿用既有的 `MetricBar`。純文字項目不受影響。
- 超過 1000 GB 以 TB 顯示，免得 1.8 TB 的硬碟寫成 1843.2 GB。

### VRAM 標籤

**每張顯卡一律顯示「VRAM」**。原本內顯標「共享記憶體」、獨顯標「顯示記憶體」，同一欄出現兩種名稱，
掃視時讀不成一件事。專用／共享是規格，移到詳情面板的「記憶體類型」。

> 取捨：內顯那 15.8 GB 其實是向系統記憶體借的，叫它 VRAM 精確度略有損失。詳情面板保留了
> 「與系統記憶體共享」這項事實，所以資訊沒有消失，只是不再干擾主畫面的掃視。

`Win32_VideoController.AdapterRAM` 後備路徑保留但標為「約略值」—— 那個欄位是 uint32，
上限 4 GB，任何 8/12/16 GB 的卡都會報錯值。只有在感測器完全讀不到時才會走到。

## 9. 顏色規則的修正（同日第三輪）

原本的「一頁一個強調色 ＝ 當下佔用最高那一列」在實機上看起來是壞的：RAM 48% 是綠的，
CPU 6%、GPU 10%、DISK 29% 都是白的。使用者的回饋是「只有 RAM 有綠色的 BAR？不協調」——
而他說得對，**問題出在規則本身**。

48% 沒有任何絕對意義，它只是那一刻剛好最大。用顏色標示「最大」，等於把一個沒有意義的事實
變成畫面上最搶眼的訊號。而且一路參考的 NZXT CAM，所有的環與條**一律是品牌藍**，從來不是只有一條。

### 現行規則

| 元素 | 顏色 |
|------|------|
| 所有粗條 | 強調色；超標時轉警示色 |
| 數值文字 | 主文字色；超標時轉警示色 |
| 英雄數字（功耗） | 強調色（它是唯一的英雄，不參與比較） |
| 圖示重點元素 | 強調色 |

超標定義：負載 ≥ `MainViewModel.AlarmPercent`（90，與提醒引擎預設一致）；
磁碟則是剩餘空間低於 `Settings.DiskFreeLimit`（預設 10%），而非用掉的百分比。

**為什麼這樣才對**：粗條共用同一條基準線，長度本身就足以互相比較 —— 那正是當初把圓環換成粗條的理由。
比較既然已由長度完成，顏色就能專心只說一件事：這個讀數需要注意。

### 隨之簡化的東西

- `BarTone` 由 `{ Normal, Hot, Alarm }` 縮為 `{ Normal, Alarm }`。
- `MainViewModel` 的 `Slot` enum、`PeakSlot`、以 slot 解同分的邏輯**全部移除** —— 那整套機制
  存在的唯一目的就是決定「誰是最高」。
- `ToneBrushConverter` 的 `ConverterParameter=muted` 分支移除（已無消費者）。
- 斷言改寫：原本驗「至多一列上強調色」，現在驗**每一列的色調都精確對應它是否超標**，
  以及磁碟只在確實接近全滿時才警示。UI 斷言數 25 → 28。

## 10. 名稱

「Momo監控」改為 **「Momo 系統監測」／「Momo System Monitor」**（`app_title`、`app_brand`、
`AssemblyTitle`、`Product`）。

> 使用者以簡體寫成「Momo 系统监测」，但應用程式的中文介面通篇為繁體（語言選單即標示「繁體中文」），
> 因此採繁體「系統監測」以保持一致。若確實要簡體，改 `Loc.cs` 兩處即可。

執行檔名維持 `MomoMonitor.exe`，設定資料夾維持 `%APPDATA%\StatusMonitor\`（改名會讓累計用電歸零）。

## 11. 驗證

- `dotnet build -c Release`：0 warning、0 error。
- `--render --verify-features`：**PASS 28 項 UI 互動斷言**（含每列色調需精確對應是否超標）。
- `tests/FeatureChecks`：**PASS 48 項核心斷言**。
- `build.ps1`：產出 `MomoMonitor.exe`（單一檔案，暫存資料夾自動清除）。
- 預覽：[VOLT 儀表板](previews/volt-dashboard.png)、[PAPER POP 儀表板](previews/paper-dashboard.png)、[精簡](previews/volt-compact.png)、[迷你](previews/volt-mini.png)、[設定](previews/volt-settings.png)、[信息頁](previews/paper-info.png)、[720 寬](previews/volt-720.png)。

非管理員測試不代表所有硬體感測器均有讀數；溫度、風扇等欄位在本機為 `—`。

## 12. 兩種模式（同日第四輪）

原本有三種模式：完整、精簡（600×600 的主視窗變體）、迷你（400×252 浮窗）。精簡與迷你顯示的
是同一批讀數，等於同一件事要跟使用者解釋兩次。**精簡已移除**，只剩完整與迷你。

### 共用一份版面，而不是各寫一份

迷你要能顯示與儀表板相同的內容，兩個視窗就必須共用版面 —— 各寫一份必然會走樣。作法：

- 圖示 `Geometry`、列與英雄的樣式、`ToneBrushConverter` 全部由 `MainWindow.xaml` **搬到 `App.xaml`**，
  因為 `MiniWindow` 取用不到另一個 `Window` 的 `Window.Resources`。
- 英雄區 ＋ 負載列包成一個 `DataTemplate x:Key="StatBody"`，兩個視窗都以
  `<ContentControl ContentTemplate="{StaticResource StatBody}" Content="{Binding}" />` 渲染。
- 密度差異由 `MiniWindow` 設 `Tag="True"` 表達，`LoadRow` 樣式的 `DataTrigger` 據此把列距
  由 `2,10,2,11` 收為 `2,6,2,6`。**只有呼吸空間不同，內容完全一樣。**

迷你視窗 400×252 → **470×600**，容得下功耗英雄、三個小格、CPU／兩張 GPU／RAM／DISK（雙磁碟分欄）／NET。

### 隨之移除

`SetCompactMode`／`ToggleCompact`／`UpdateCompactLabel`／`CompactButton`、`_fullWidth`／`_fullHeight`／
`_fullState`、`AppSettings.CompactMode`、`CompactOnly`／`FullOnly` 樣式、`mode_compact`／`mode_full`
語言字串、`--compact` 算繪參數。

`App.xaml.cs` 的儀表板算繪斷言原本檢查多欄卡片網格的間距與列高 —— 那套東西在單欄列表下已無意義，
改為檢查**所有負載粗條的左緣是否對齊同一個 x**，那才是現在版面真正的不變量。

## 13. 語言與設定圖示

- 頁首的「中／EN」按鈕移除；設定面板本來就有語言下拉選單，不需要兩個入口。
- 設定齒輪由文字符號 `⚙` 改為向量 `Geometry`（`IcoGear` ＋ `IcoGearTop`），與各列圖示同一套
  24 單位網格與筆畫語言。文字符號的外觀取決於系統解析到哪個字型，向量則不會。

## 14. 迷你面板重做為速覽（同日第五輪）

§12 讓迷你顯示與儀表板**完全相同**的內容（共用 `StatBody`），結果是 470×600 —— 那不是迷你，
是縮小版的儀表板。使用者的回饋是「太大了、不要太多餘的 BAR、可以看 ICON 不用文字」。

### 現行：258 × 238

| 原本 | 現在 |
|------|------|
| 每列：圖示 ＋ 型號文字 ＋ 橫條 ＋ 百分比 ＋ 副標 | 每格：**圖示 ＋ 數值** |
| 巨型功耗英雄（92px） | 功耗 28px，仍是唯一有份量的數字 |
| 累計／碳／電費三個獨立小格 | 一行安靜的文字 |
| DISK 雙磁碟分欄 | **最滿的那一顆**（`DiskPeakText`） |
| 共用 `StatBody` 模板 | 自己的緊湊版面 |

**為什麼拿掉橫條**：橫條的價值來自共用基準線後可以互相比長度 —— 那是儀表板的事。
在只有兩欄、每欄一個數字的面板裡，橫條只是佔空間的裝飾。

**為什麼用圖示取代文字標籤**：`11th Gen Intel Core i5-11400` 這種字串在 258 寬裡必然被截斷成
`11th Gen Inte…`，讀不出資訊又佔掉整行。圖示能識別、且只佔 20 DIP；完整型號在儀表板與信息頁都有。

### 一個排版陷阱

第一版把圖示靠格子左緣、數值靠右緣，於是兩欄並排時掃過去讀成
「12%　[RAM 圖示]　43%」—— 看起來像四欄交錯而不是兩組配對。

解法是讓圖示與數值**相鄰**（`StackPanel` 橫向），再靠數值的 `MinWidth="54"` ＋ 右對齊維持欄內對齊。
配對關係由鄰近性建立，對齊由最小寬度建立，兩者不衝突。

迷你仍沿用 `App.xaml` 的圖示、色票與字體，所以換皮、超標變色的行為與儀表板完全一致。

## 15. 頁首去重與提醒記錄搬家（同日第六輪）

### 品牌名不再重複

標題列已經染成配色、也顯示了「Momo 系統監測」，頁首再寫一次 21px 的同一個名字是純粹的重複。
參考 NZXT CAM：它的視窗上緣有品牌，內容區就直接開始，不再複述。

- 頁首移除品牌文字與版本徽章。
- 導覽（儀表板／信息）改為**靠左**，也就是視線開始的地方；迷你與設定留在右側。
- 品牌與版本移入設定面板標題列（`設定　Momo 系統監測　v0.1.0`），需要時查得到，平時不佔版面。

### 最近提醒移入設定

原本掛在趨勢卡下方，是一個**幾乎永遠收合且空的** Expander。但它的功能有存在意義：
Windows 的勿擾模式會壓掉系統匣通知，這是唯一還看得到「什麼時候觸發過什麼」的地方。

所以不是刪掉，而是移到**設定 →「監控與提醒」**，緊接在門檻欄位與「套用提醒設定」之後 ——
設門檻和查記錄是同一件事的兩面，放在一起才找得到。儀表板因此少掉一塊常年空白的區域。

## 16. 品牌 lockup 回來了（§15 修正）

§15 把頁首的品牌整個移除，理由是「標題列已經顯示了名字」。實機上這個判斷是錯的：

原生標題列只給 **16px 圖示 ＋ 系統字級文字**，那個尺寸傳達不了任何識別 ——
水豚在 16px 下是一團色塊。NZXT CAM 之所以看起來品牌明確，是因為它用**自繪 chrome**，
在自己的視窗上緣放完整的 logotype；那不是「重複」，那就是 logo 本身。

所以區分清楚：**重複的是純文字名稱，不是 logo。** 頁首恢復為

```
[水豚 28px]  Momo 系統監測  │  儀表板  信息 ............ 迷你  ⚙
```

版本號仍留在設定面板，不讓它污染 lockup。標題列維持原生（染色），因為自繪 chrome 要付出
Snap Layouts 與 DPI 的代價 —— 見 §7。

### §16 再修正：只放標記，不放品牌字

加了 wordmark 之後名稱仍然出現兩次（標題列一次、頁首一次），使用者的判斷是對的。
三個版本走完才收斂：

| 版本 | 結果 |
|------|------|
| 完全移除品牌 | 名稱只剩標題列 16px —— IP 消失 |
| 標記 ＋ wordmark | IP 明確，但名稱重複兩次 |
| **只放標記** | **標記在頁首、名稱在標題列，各說一次** |

水豚放大到 30px，標題列的 16px 圖示做不到的識別由它負責；名稱交給標題列。

> 若日後想要 CAM 那種「品牌字直接在視窗上緣、且只出現一次」的效果，正確作法是 WPF 的
> `WindowChrome`（延伸內容到標題列、保留系統按鈕），而不是 `WindowStyle="None"` 全自繪。
> §7 說「自繪會失去 Snap Layouts」只適用於後者 —— `WindowChrome` 搭配 `UseAeroCaptionButtons`
> 會保留原生按鈕與 Snap Layouts。這條路沒走，是因為它與現行的 DWM 標題列染色會互相干擾。

## 17. 導覽分頁與品牌的最終位置

### hover 時選中分頁會消失（bug）

`SetNav` 用**本地值**指定 `Background` / `Foreground`，而模板的 hover 觸發器改的是模板內部
具名元素 `bd` 的背景。WPF 的優先序讓這兩者各贏一半：

- `Foreground`：本地值勝過模板觸發器 → 維持深色（`OnAccentBrush`）
- `bd.Background`：針對具名元素的 setter 蓋掉 `TemplateBinding` → 變成深色 `HoverBrush`

結果是**深色字配深色底**，選中的分頁滑過去就整個看不見。

修法不是調顏色，是把選擇改成**狀態**：`SetNav` 只設 `b.Tag = "on"`，所有顏色由模板觸發器決定。
hover 觸發器宣告在前、`Tag` 觸發器在後，所以選中態永遠贏得前景色 —— 不存在深配深的路徑。

新增三條斷言鎖住它：兩個分頁都不得帶本地 `Foreground` / `Background`，且選中的必須是 ink、
未選的必須是 muted。UI 斷言 26 → 29。

### 選中態＝底線

原本是填滿的 chip，比它上方的讀數還搶眼。改為 2.5px 的強調色底線 ＋ 文字轉 ink 加粗，
未選維持 muted。分頁是導覽，不該跟資料搶注意力。

### 品牌集中到設定

頁首只留導覽與操作。品牌標記（34px 水豚）、名稱與版本集中在設定面板標題列 ——
識別資訊看一次就夠，不必每一幀都佔著主畫面。標題列仍顯示名稱，所以名稱依然只出現一次。

## 18. 六項細修

| # | 問題 | 做法 |
|---|------|------|
| 1 | 「估算功耗」貼著下方數字 | 標籤下緣留白 3 → 8 |
| 2 | CPU 型號的文字顏色看起來和其他次要文字不同 | **色票其實完全相同**（都是 `#9A978C`，已逐像素驗證）。差異來自渲染：`估算功耗` 是 9px 粗體，型號是 10px 等寬細體，深色底上 ClearType 會給細筆畫強烈色邊。改為 `TextRenderingMode="Grayscale"`，三種次要文字的渲染方式一致 |
| 3 | 迷你是文字按鈕 | 改為子母畫面圖示（外框 ＋ 右下小框） |
| 4 | 齒輪不像常見的設定圖示 | 原本是八根放射線（Feather 風格），讀起來像太陽。改為**真正有齒的齒輪**，以腳本在 24 單位網格上生成 8 齒輪廓（外徑 10.6、內徑 7.9） |
| 5 | 迷你／設定鍵在淺色皮上有白底塊 | `IconButton` 去掉填色與邊框，只靠 hover 提供底色 |
| 6 | 歷史趨勢的色塊深淺皮不一致 | 趨勢／風扇／處理程序三區一律改為**細線分隔**（`SectionRule`），不再填色。原本的填色在深色皮幾乎看不見、在淺色皮卻是一整塊白 —— 同一個元件讀起來像兩種東西 |

### 順帶修掉一個我自己種的 bug

頁首與迷你面板的圖示外框我寫成 `Grid Width="20" Height="20"`，但圖示幾何是 **24 單位** ——
超過 20 的部分直接被裁掉，所以迷你圖示的右邊框不見、齒輪右下被切。

修法是包一層 `Viewbox Width="20"` 再放 24 單位的 `Grid`：整組等比縮放，兩層仍共用同一座標系。
這和 §7 記錄的 `Stretch` 陷阱是同一類問題 —— **縮放要作用在整組上，不是個別 Path，也不是靠裁切**。

## 19. 關閉行為改為可設定

使用者回報「按 CLOSE 只關視窗，程式還在跑」。實際查下來，✕ 本來就會結束程式 ——
真正造成困惑的是**最小化**：`StateChanged` 一律在最小化時 `Hide()` 到系統匣，視窗就消失了，
從使用者角度看就是「關不掉」。**一個沒被詢問過就消失進系統匣的視窗是意外，不是功能。**

參考其他 App 的慣例（Discord／Slack／Teams／Steam／NZXT CAM 都有同類開關）：

- 新增 `AppSettings.RunInTray`，**預設 `false`**。
- 關閉時：✕ 直接結束程式；最小化就是一般的最小化到工作列。
- 開啟時：✕ 與最小化都只隱藏，背景取樣繼續；雙擊系統匣圖示還原。
- 系統匣右鍵的「結束程式」永遠可用，且走 `ExitApplication()` → `_forceExit = true` → `Close()`，
  所以**不會被自己的設定困住**，也仍然走正常關閉流程把累計用電存檔。

設定位置：設定 →「一般」，勾選框下方一行說明講清楚預設行為與逃生口。
