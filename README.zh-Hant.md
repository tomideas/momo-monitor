# Momo 系統監測 · Momo System Monitor

**English | [繁體中文](README.zh-Hant.md)**

Windows 11 桌面小工具：即時顯示整機狀態（CPU / GPU / RAM / 網路 / 儲存 / 風扇與水冷幫浦）、
瓦數、累計用電、碳足跡、電費，以及 Top Processes。視覺參考開源專案 WattSeal（Rust，GPLv3）。
介面為 **VOLT / PAPER POP** 雙皮設計：一頁一個英雄數字（目前功耗）、共用基準線的粗條列表、
顏色只用來標示超標；保留水豚應用程式圖示，頁首使用純文字品牌。設計說明見
[docs/2026-09-13-volt-paper-pop.md](dev/docs/2026-09-13-volt-paper-pop.md)。

- 技術：WPF (.NET 8)，單一程序，以管理員身分執行
- 語言：English / 繁體中文（可切換）
- 更新頻率：每 1 秒
- 硬體感測：自行建置的 **LibreHardwareMonitor master** ＋ **PawnIO** 驅動（見下）
- 分頁：**儀表板**（即時狀態）／**信息**（硬體與系統資訊，WMI）

## 外觀與背景

設定 → 一般 → 背景配色，可選 **Volt 夜跑**（深色，螢光強調）或 **Paper Pop 日間**（米白，鈷藍強調）。
兩者共用同一套排版骨架，只有色票不同；即時套用完整、精簡與 Mini 模式，重新開啟仍保留選擇。
舊版七個背景設定仍可讀入（深海藍 → Volt，其餘 → Paper Pop）。

英雄數字與百分比採嵌入的 **Barlow Condensed** Black Italic（真斜體，含 tabular figures，每秒更新不位移），
次要數值維持 Geist Mono，UI 標籤為 Geist；長數字自動縮小以符合可用寬度。

**顏色只說一件事：這個讀數超標了。** 所有粗條一律使用強調色，因為它們共用同一條基準線，
長度本身就足以互相比較；任一項達到 90%（磁碟則是剩餘空間低於門檻）時該條改用警示色。
數值文字維持主文字色，同樣只在超標時變色。

## 趨勢、迷你模式與提醒

- **只有兩種模式**：完整視窗與迷你速覽浮窗。原本介於兩者之間的「精簡模式」已移除。
- **迷你浮窗**：258 × 238 的速覽面板 —— 圖示代替文字標籤、數值代替橫條，只留功耗、CPU／GPU／RAM／最滿磁碟／網路。頂部有拖曳握把，**面板任意處皆可拖曳**（按鈕與置頂勾選框除外），雙擊任意處回主視窗；仍可置頂。

- **趨勢圖**：60 秒／15 分鐘範圍，CPU／GPU 負載、RAM、GPU 記憶體、CPU／GPU 溫度及估算功耗。滑過看時間與數值，顯示最低／平均／最高；只保留本次啟動最近 15 分鐘，重啟不保留。感測缺失、取樣中斷保留空段，不補成 0。
- **迷你模式**：頁首「迷你」開啟；頂部握把提示可拖曳，面板任意處皆可拖曳移動；雙擊任意處或按右上角箭頭回主畫面。同步顯示 CPU、主 GPU、GPU 記憶體及估算功耗，可切換置頂；位置、置頂、啟動時開啟迷你模式會保留。
- **系統匣**：**預設關閉** —— 按右上角 ✕ 直接結束程式，最小化就是一般的最小化到工作列。設定 →「一般」可勾選「關閉或最小化時留在系統匣繼續執行」；開啟後 ✕ 與最小化都只是隱藏，背景取樣與提醒繼續運作，雙擊系統匣圖示還原。系統匣右鍵選單永遠有「結束程式」當逃生口，且走正常關閉流程（累計用電會存檔）。
- **主 GPU**：設定 →「監控與提醒」。自動模式優先獨立顯卡，可手動指定，套用於第一張卡、GPU 趨勢、迷你模式及 GPU 提醒。以感測器識別碼保留選擇；原卡不可用時暫用自動選擇。
- **隱藏內顯**：僅在有獨立顯卡時隱藏已辨識的內顯；只剩內顯時仍會顯示。手動選內顯會取消隱藏；未知型號保守保留。可讀到共享記憶體時標示 Shared／共享記憶體。
- **超標提醒**：預設 CPU 90°C、主 GPU 85°C、RAM／GPU 記憶體 90%、固定磁碟剩餘空間 10%，持續 15 秒才觸發；每次異常只通知一次，恢復後再超標仍受 300 秒冷卻限制。門檻與時間可修改，需按「套用提醒設定」。
- **提醒記錄**：設定 →「監控與提醒」分頁下方，保留本次啟動最近 20 筆。Windows 系統匣通知是否顯示取決於系統通知／勿擾設定；通知受抑制時仍有程式內記錄。
- **資料來源限制**：溫度、顯存等依感測器支援；不存在的數值不觸發提醒。磁碟剩餘空間每 30 秒查詢一次。歷史統計為有效取樣值的統計，功耗與用電仍包含估算。

驗證：48 項核心斷言、14 項 UI 互動斷言、20 秒實際取樣累積，以及中英文／720 DIP 畫面檢查均通過；非管理員測試不等於所有硬體感測器支援驗證。系統匣初始化與釋放已測試，未用測試警報打擾使用者。

```powershell
& "$env:LOCALAPPDATA/MomoBuild/dotnet/dotnet.exe" run --project dev/tests/FeatureChecks/FeatureChecks.csproj -c Release
# 以下使用非管理員測試版，且 --render 使用暫時設定，不覆寫原設定／累計。
.\StatusMonitor\bin\Release\net8.0-windows\MomoMonitor.exe --render features.png --verify-features --trends
```

預覽：[實際趨勢](dev/docs/previews/features-trend-live.png)、[迷你模式](dev/docs/previews/features-mini.png)、[監控設定](dev/docs/previews/features-settings.png)。

## 2026-09-13 介面改版：VOLT / PAPER POP

- **英雄數字改為目前功耗**。用電、碳足跡、電費是這支工具獨有的讀數，CPU 百分比則每個監控軟體都有；累計用電／碳足跡／電費降為英雄旁的三小格。
- **圓環全數移除**（`GaugeControl` 已刪除）。CPU／GPU／RAM／DISK 改為共用同一條基準線的粗條列表，長度直接可比；NET 維持速率讀數。完整與精簡模式皆為單欄。
- **顏色的意義收斂成一條規則**：所有粗條一律用強調色，只有超標時才轉警示色。中途曾試過「只有當下最高那條上色」，但那讓顏色變得沒有道理 —— RAM 48% 之所以是綠的，不是因為 48% 有什麼特別，只是它剛好最大；畫面上一條綠配四條白，看起來像壞掉。粗條共用同一條基準線，長度本來就足以比較，顏色因此可以專心只說「超標」。
- **背景配色由七個縮為兩套皮**（Volt 夜跑／Paper Pop 日間），共用同一套排版骨架。舊設定自動映射。
- 嵌入 Barlow Condensed Black Italic 作為顯示字體；字級階梯由 9 拉到 92。
- 修正：淺色主題下 accent 與 ink 同為墨黑（等於沒有強調色）、信息卡標題渲染成黑色遮蔽條、`PopPaper` 網點被純色覆蓋、`TextBox`／`ComboBox`／`TabItem` 顏色寫死、自訂 ComboBox 模板下選取值顯示成匿名物件、切換精簡模式後儀表板散成多欄網格。
- 名稱改為「**Momo 系統監測**」／「**Momo System Monitor**」。
- 每列加上 24 DIP 兩色圖示（CPU／GPU／RAM／DISK／NET）：粗圓筆畫輪廓 ＋ 一個重點元素，標籤字級由 10 放大到 12。純向量 `Geometry`，不依賴圖示字型，兩套皮自動換色。重點元素一律使用強調色（Paper Pop 藍／Volt 螢光綠），整欄讀起來是同一套。
- 迷你視窗：頂部新增拖曳握把，且**整個面板皆可拖曳**（原本只有標題列那條細帶可拖，也沒有任何提示）；還原鈕與置頂勾選框仍正常點擊。
- **信息頁儲存卡重整**（參考 NZXT CAM）：所有固定磁碟收進**同一張卡**，每顆一列，顯示「已用 X / Y（Z%）」＋ 橫條，並帶磁碟區標籤。原本每顆硬碟各自一張卡，且數值是「剩餘／總量」卻只標「空間」，容易被讀反。
- **儀表板 DISK 列改為空間佔用率，且每顆硬碟各自一格**（參考 NZXT CAM）：多顆硬碟在同一列內左右均分，各有橫條、百分比與已用／總量，不新增卡片也不新增列。刻意不做總計 —— 平均會蓋掉那顆快滿的。原本顯示的是 `% Disk Time`（I/O 忙碌率），`0%` 代表「沒在讀寫」而非「硬碟是空的」，容易誤解。磁碟以剩餘空間低於門檻（預設 10%）作為警示條件，而非用掉的百分比。
- **顯卡記憶體一律標示 VRAM**，不再分「共享記憶體」／「顯示記憶體」兩種名稱；並顯示已用／總量與橫條。專用或共享移至詳情面板的「記憶體類型」。
- **次要文字對比度修正**。原本的灰在深色皮上量到只有 4.21:1（Paper Pop 4.44:1），低於 WCAG AA 小字門檻 4.5，而那些標籤是 10px 等寬字。調整後兩套皮分別為 6.73:1 與 6.59:1 —— 能讀，但仍明顯低於主要文字，層級不變。驗證是從算繪的 PNG 逐像素量字芯，不是目視。
- **視窗標題列隨配色上色**（參考 NZXT CAM）。Volt 皮為 `#0B0B0B` 配白字，Paper Pop 為 `#EDEAE1` 配黑字，切換配色即時重新上色。用 Windows 11 的 DWM 屬性染色**原生**標題列，而非自繪 chrome，因此 Snap Layouts、拖曳、貼齊、DPI 行為全部保留。Windows 10 以下會忽略，標題列維持系統預設。
- **產物只剩 `MomoMonitor.exe` 一個檔案**。`<AssemblyName>` 改為 `MomoMonitor`，`build.ps1` 不再把同一份二進位複製成第二個檔名，也移除了「被鎖住就寫 `.new.exe`」的退路 —— 現在直接報錯要求先關掉程式。設定資料夾刻意維持 `%APPDATA%\StatusMonitor\`，改它會讓現有累計用電歸零。C# 命名空間（`RootNamespace`）也維持不變。
- **頁首只放導覽**；品牌標記、名稱與版本集中在設定面板（34px 水豚 ＋ 名稱 ＋ 版本）。
- **分頁選中態改為底線**，不再是填滿色塊 —— 實心 chip 比它上方的讀數還搶眼。同時修掉一個 hover bug：選中的分頁滑過去會變成深色字配深色底、整個看不見。原因是選擇狀態是用**本地值**指定顏色，與模板的 hover 觸發器互相蓋 —— 改為以 `Tag` 驅動狀態，顏色全由模板決定。新增三條斷言鎖住這件事。
- **「最近提醒」移入設定 →「監控與提醒」**。它原本掛在趨勢區，是一個幾乎永遠收合且空的區塊；但勿擾模式會壓掉系統匣通知，這是唯一的程式內記錄，所以不刪、只是搬到查得到的地方。
- **簡化為兩種模式**：完整與迷你，移除中間的「精簡模式」。迷你重做為 258 × 238 的速覽面板：圖示代替文字標籤、數值代替橫條（橫條只有在能互相比長度時才有價值），磁碟取最滿的一顆。
- **語言切換移入設定**，頁首不再有「中／EN」按鈕。設定齒輪改為向量圖示，與各列圖示同一套筆畫語言，不再是仰賴系統字型的文字符號。
- 細修：「估算功耗」與英雄數字留白加大；次要文字統一為灰階反鋸齒（色票本來就相同，差異來自 ClearType 對細筆畫的色邊）；迷你改為子母畫面圖示；設定改為真正有齒的齒輪；頁首圖示鍵去掉底色塊；趨勢／風扇／處理程序三區改為細線分隔，深淺兩皮一致。
- 驗證：Release 建置零警告零錯誤；29 項 UI 互動斷言、48 項核心斷言皆通過。
- 預覽：[VOLT 儀表板](dev/docs/previews/volt-dashboard.png)、[PAPER POP 儀表板](dev/docs/previews/paper-dashboard.png)、[設定](dev/docs/previews/volt-settings.png)、[信息頁](dev/docs/previews/paper-info.png)。

## 2026-09-12 介面更新

- 儀表板 CPU、GPU、記憶體、網路及儲存共用同一自適應網格；末列不足時平均延展填滿，避免不同視窗寬度或 GPU 數量造成缺口。已驗證 720 / 900 / 1040 / 1440 寬度與模擬 0 / 1 張 GPU 的版面。

- 捲軸改為 4 DIP 淡灰滑塊（10 DIP 操作區），滑過或拖曳時加深；處理程序列表取消內部捲軸及高度上限，隨整頁捲動。

- 中文名稱「Momo 系統監測」、英文名稱「Momo System Monitor」，視窗標題及頁首隨語言切換。
- 信息卡統一 280 DIP 高、最多三項摘要；依視窗寬度切換欄數。完整規格可開啟詳情面板並複製，Esc 關閉。
- 信息頁只於啟動、進入、切換語言或手動刷新時在背景查詢規格，不再每秒清空重建。
- 黃色選中分頁、暖白網點背景、粗框硬陰影；分頁及詳情入場動效、儀表和進度條平滑更新。設定可減少動效，並尊重 Windows 動效設定。
- 儀表板 CPU/GPU 區域可自動換欄，無感測功耗顯示「—」，總功耗標示為估算。
- 修正設定欄位電價／碳強度／程序數未接上儲存事件。
- 舊設定與累計仍讀取 `%APPDATA%\StatusMonitor\`（刻意保留，改名會讓現有累計歸零）。
- 已驗證 Release 建置零警告零錯誤；720 / 1040 寬信息頁卡片尺寸斷言、中英文畫面、詳情及設定 PNG 渲染。非管理員測試不代表所有硬體感測器均有讀數。
- 預覽：[中文信息頁](dev/docs/previews/info-zh.png)、[英文儀表板](dev/docs/previews/dashboard-en.png)、[詳情](dev/docs/previews/details-en.png)。

## 資料來源

| 元件 | 來源 |
|------|------|
| CPU | LibreHardwareMonitor（溫度 Tctl/Tdie、套件瓦數、各核心時脈） |
| 風扇／水冷幫浦 | LibreHardwareMonitor（主機板 SuperIO，如 Nuvoton NCT6701D） |
| GPU | LibreHardwareMonitor（NVIDIA ＋ AMD ＋ Intel 內顯；每張卡：溫度、時脈、風扇 RPM、瓦數、負載、VRAM used/total；Dashboard 頂部依主 GPU 設定顯示前 2 張，自動優先独立顯卡） |
| RAM | GlobalMemoryStatusEx |
| 儲存 | `PhysicalDisk` 效能計數器 |
| 網路 | `NetworkInterface` 統計 |
| 每程式 | Process API ＋ `GPU Engine` 計數器 ＋ GetProcessIoCounters |

瓦數與每程式分攤公式移植自 WattSeal（GPLv3）。

## 前置需求：PawnIO

新版 LibreHardwareMonitor 使用 **PawnIO**（開源、有簽章的核心驅動）做低階硬體存取，
取代被 Microsoft 封鎖的 WinRing0。**首次使用需一次性安裝**（管理員）：

```powershell
winget install namazso.PawnIO -e
```

未安裝 PawnIO 時，CPU 溫度 / 風扇 / 板載電壓會顯示 `—`，其餘功能正常。

## 建置

需要 **.NET 8 SDK**（若重建 `libs/` 內的 LibreHardwareMonitor master，另需 .NET 9 SDK）。
若尚未安裝，可用官方腳本安裝到使用者目錄（免系統管理員）：

```powershell
Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile $env:TEMP\dotnet-install.ps1
& powershell -NoProfile -ExecutionPolicy Bypass -File $env:TEMP\dotnet-install.ps1 -Channel 8.0 -InstallDir "$env:USERPROFILE\.dotnet"
```

產出單一 exe：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

產物為 `MomoMonitor.exe`（framework-dependent，執行端需 .NET 8 Desktop Runtime）。

> 因為專案位於網路路徑，`build.ps1` 會先複製到本機暫存資料夾再編譯。產物**只有
> `MomoMonitor.exe` 這一個檔案**；若它正在執行而被鎖住，建置會直接報錯要求你先關掉程式
> （從系統匣結束，累計用電才會存檔），不會另外產生第二個檔名。
>
> 暫存資料夾（每次約 165 MB）在建置結束時自動刪除，**失敗時也會清**；啟動時另會掃掉一小時前
> 遺留的舊資料夾。要保留以便除錯，加上 `-KeepWork`。
>
> `StatusMonitor\libs\` 內為自行建置的 LibreHardwareMonitor master 及其相依 DLL。

## 執行

雙擊 `MomoMonitor.exe`，會出現 UAC 提示（讀取硬體感測器需要管理員權限）。
若沒有管理員權限，溫度 / 風扇 / CPU 瓦數會顯示 `—`，其餘功能正常。

## 設定

設定（齒輪圖示）可調整：

- 語言：English / 繁體中文（原本在頁首的「中／EN」按鈕已移入此處）
- **國家／地區（單一選單）**：選一次同時套用電價、幣別與碳強度
  （France / Germany / UK / USA / China / India / Sweden / Poland / World average / Custom）
- 電價（＋幣別）與碳強度可再手動微調；手動改值後國家自動變為 Custom
- 顯示程序數、重置累計用電

### 資料檔案

預設存在 `%APPDATA%\StatusMonitor\`，共三個：

| 檔案 | 內容 |
|---|---|
| `settings.json` | 所有偏好設定 |
| `totals.json` | 累計用電（Wh），跨重啟 |
| `energy-history.json` | 每日用電，保留約 400 天，「今日／7 天／30 天」由它算出 |

**可攜模式**：在 `MomoMonitor.exe` 旁邊建一個 `momo-data` 資料夾，這三個檔案就改存在裡面，隨程式一起搬移、備份。沒有這個資料夾則行為不變。第一次以可攜模式啟動時，`%APPDATA%` 裡既有的資料會自動複製過去（只複製到空資料夾，不會覆蓋）。

## 驗證（無介面）

```powershell
.\MomoMonitor.exe --dump --out snapshot.json   # 取樣一次輸出 JSON
.\MomoMonitor.exe --diag --out diag.txt        # 列出所有可見硬體感測器
.\MomoMonitor.exe --render dash.png            # 把儀表板算繪成 PNG
```

## 已知限制

- 主機板風扇／幫浦來自 **SuperIO**，其支援取決於主機板型號是否有對應的 LibreHardwareMonitor
  晶片驅動（本機 ASUS ROG STRIX B850-I 為 Nuvoton NCT6701D，已支援）。不支援的板子會顯示 `—`。
- **GPU 風扇**以 RPM 顯示（LibreHardwareMonitor `GPU Fan 1`）；無風扇感測器的卡（如 AMD 內顯）顯示 `—`。
- 非管理員執行時，溫度/風扇/CPU 瓦數顯示 `—`，其餘功能正常。
- 單檔 exe 為 framework-dependent，執行端需安裝 .NET 8 Desktop Runtime。

## 設計文件

完整設計（架構、公式、驗收）見 [`dev/docs/2026-09-12-status-monitor-design.md`](./dev/docs/2026-09-12-status-monitor-design.md)。
UI 重新設計規格（light neo-brutalism、嵌入字型、配色 token、卡片雙語標題）見 [`dev/docs/2026-09-12-status-monitor-ui-redesign.md`](./dev/docs/2026-09-12-status-monitor-ui-redesign.md)。
Dashboard 多 GPU 固定 3 欄設計（每卡 VRAM、CPU 核心數 / 基準速度）見 [`dev/docs/superpowers/specs/2026-09-12-dashboard-multi-gpu-cpu-baseinfo-design.md`](../../docs/superpowers/specs/2026-09-12-dashboard-multi-gpu-cpu-baseinfo-design.md)。








