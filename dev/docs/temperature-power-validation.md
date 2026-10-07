# 溫度與功耗跨機驗證 / Cross-computer temperature and power checks

0.1.6 修正了取值與加總邏輯，並提供當次感測 CSV；尚未完成多部筆電與桌機的實體準確度測試。
自動檢查驗證的是缺值、來源、計算與匯出行為，不能證明感測器本身的精度。

## 優先測試矩陣

| 類型 | 必看來源 | 主要情境 | 狀態 |
|---|---|---|---|
| 目前 AMD 桌機＋NVIDIA 獨顯 | CPU Tdie/Package、GPU board、風扇來源 | 閒置、穩定負載、睡眠恢復 | 需實機對照 |
| Intel 內顯筆電（含 Core Ultra） | CPU package、Intel GT、電池 rate | AC／離電、螢幕亮度變化 | 待測 |
| AMD APU 筆電 | Tdie/Tctl、package、APU/SoC 範圍、電池 rate | AC／離電、睡眠／喚醒 | 待測 |
| Intel/AMD＋NVIDIA 混合顯示筆電 | CPU package、dGPU board、有效 0 W／缺值 | dGPU 休眠、遊戲、拔插電源 | 待測 |
| AMD 獨顯桌機 | GPU board/PPT/core、hot spot、memory junction | 閒置、負載、多 GPU | 待測 |
| 雙電池或特殊韌體機型 | 每電池絕對／相對單位、方向與缺值 | 同向放電、混合充放電 | 待測 |

## 同一來源才可比較

1. 記錄 Momo、HWMonitor、Windows 與 GPU 驅動版本；記錄 CPU/GPU 型號、電源方案及 AC／離電狀態。不要把主機名、序號或帳號放入測試資料。
2. 兩套程式開在同一台電腦，等待更新穩定。首頁點 CPU／GPU，展開感測明細，確認兩邊比較相同來源與範圍。
3. 閒置與穩定負載各觀察至少 60 秒；於同一時刻保存 HWMonitor 畫面／讀值與 Momo 感測 CSV。即時讀值會因取樣時刻、更新頻率與廠商平均窗口而不同，單次差值不能代表整段平均差。
4. CPU 優先比對 Tdie／Package；Tctl 的控制偏移、單一核心、CCD 或 Distance to TjMax 不是等價數值。GPU core、hot spot 與 memory junction 也要各別比對。
5. GPU 功耗比對整卡／board 等價範圍，不把 core、SoC、rails 再加到 board。筆電 AC 的 CPU＋GPU 小計不等於整機。充電率是存入電池的能量，不等於適配器輸入功耗。
6. 拔電後比對電池放電率；它較接近電池端整機負載，仍不等於牆上 AC 功率。AC 整機測量需外接電表；桌機 PSU 曲線仍屬估算。
7. 睡眠／喚醒、休眠、重啟、拔插 AC、dGPU 進出休眠各測一次。Momo 應保留缺口、恢復新讀值，不把整段中斷按最後一次瓦數累計。

可先把「相同來源穩定溫度平均差 2–3°C、功耗平均差在 2 W 或 5% 的較大者以內」作為工程調查門檻。
這是初始驗證目標，並非硬體精度規格或所有機型的保證；超過時先追查來源與時間窗口，再判定後端支援问题。

## 記錄建議

每個 case 記錄：機型類別、CPU/GPU 型號、來源名稱／範圍、AC 狀態、更新間隔、負載、Momo/HWMonitor 讀值、
差值、缺值原因、sleep/resume 前後累計與 CSV 檔名。狀態可用「通過／來源不等價／不支援／待調查」，
不支援應保持 `—`，不要用桌機 TDP 或其他測點補成量測值。

## 後端版本與界線

目前內附 LHM DLL 的檔案版本為 0.9.6.0，原專案記載由 upstream master 自行建置，加入 NCT6701D/B850 支援；
沒有保存原始 commit，所以不宣稱等同官方 0.9.6 tag。CSV 帶入該二進位 SHA256，Portable 建置會檢查指紋一致。
新硬體若 HWMonitor 有值而 LHM 沒有，先保存兩份來源證據，再評估上游更新或新增後端。不能從相鄰測點推造熱點／接面值。

官方參考：[HWMonitor](https://www.cpuid.com/softwares/hwmonitor.html)、
[LibreHardwareMonitor releases](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases)、
[Windows BATTERY_STATUS](https://learn.microsoft.com/en-us/windows/win32/power/battery-status-str)、
[Windows BATTERY_INFORMATION](https://learn.microsoft.com/en-us/windows/win32/power/battery-information-str)、
[QueryUnbiasedInterruptTime](https://learn.microsoft.com/en-us/windows/win32/api/realtimeapiset/nf-realtimeapiset-queryunbiasedinterrupttime)。
