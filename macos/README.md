# Momo Monitor for macOS — 初稿

獨立的原生 macOS 版本，所有 Mac 原始碼、資源、建置與驗證都在本目錄。
目前支援目標為 **Apple Silicon、macOS 15+**；已在目前這台 Apple Silicon
Mac 的 macOS 27.0.1 上建置及驗證，其他機型與 macOS 15/26 尚未實機驗證。

## 建置與開啟

需要 Swift 6 工具鏈（Xcode 或 Command Line Tools）及 Python 3，不需下載套件。

```sh
cd macos
bash build.sh --open
```

產物為 `build/Momo Monitor.app`。也可在 Finder 中直接開啟。
建置使用原生 `swiftc` / `clang`，不依賴本機的 Swift Package manifest runtime。
本機工具鏈混有舊版 PackageDescription 私有介面，因此初稿採用此建置路徑。
產物目前只有本機 ad-hoc 簽章；對外分發前仍需 Developer ID 與 Apple 公證。

## 已實作

- 選單列靜態水豚＋目前瓦數；左鍵開能耗面板，右鍵開選單。
- 系統／電池端／DC 輸入功耗明確分範圍；可用時獨立顯示充電功率。
- 今日同範圍累計 Wh、已監測時間、自訂電價的估算電費、五分鐘趨勢。
- CPU 使用率、記憶體占用；有內建電池才顯示電池列。
- 可拖移的水豚 Mini 浮窗、雙擊還原、隱藏、選配置頂和位置記憶。
- 原有啟動、Mini 互動與拖移動畫；各 66 幀、6.6 秒，保持素材與透明度。
- PAPER POP / VOLT、中英文、減少動效；低耗電模式自動停播。
- 設定草稿的儲存／取消、非負電價驗證、資料錯誤提示与重試。

登入自動啟動、CPU/GPU 分項瓦數、風扇控制、每程序瓦數、長期報表和自動更新
尚未實作。初稿保留 `--background` 參數，供日後登入啟動整合使用。

## 資料與功耗限制

採樣每兩秒一次，只讀取硬體，不要求 sudo，也不安裝驅動或常駐 helper。
優先使用 AppleSMC 的 PSTR；備選為 IOKit SystemLoad。只有離電且來源可用
才使用電池放電功耗，插電時的 PDTR/SystemPowerIn 則明確標為 DC 輸入。
AppleSMC 鍵與部分 registry 欄位屬於非穩定公開契約，支援取決於硬體與系統。
Registry 更新可能慢於採樣速度；跨機型更新頻率與精度仍需實測。
沒有讀值時顯示 `—`，不以 CPU 使用率或充電器額定功率填補。

DC 輸入可能包含充電；系統功耗和電池端功耗各有自己的範圍。任何一種讀值
都不能直接保證涵蓋完整插座用電、電源轉換損耗或外接螢幕。
電費只是同範圍用電乘使用者電價，不代表實際帳單。

資料只保存在 `~/Library/Application Support/MomoMonitorMac/` 的
`preferences.json` 與 `energy.json`，與 Windows 資料分離。
歷史每 30 秒、退出與睡眠前原子儲存；異常終止可能遺失最後 30 秒。
睡眠、來源切換與缺值不補算，未啟動程式時不累計。
無法讀取既有歷史時保留原檔並停止覆寫，介面提示錯誤及重試。

## 驗證

```sh
bash test.sh
'build/Momo Monitor.app/Contents/MacOS/MomoMac' --probe --samples 6
'build/Momo Monitor.app/Contents/MacOS/MomoMac' --verify-ui "$PWD/verification"
```

`test.sh` 用獨立原生檢查程式驗證能耗、午夜分段、缺值、來源變更、睡眠、
重啟、電池有號電流和電價。`--probe` 只輸出白名單的採樣欄位，不保存用戶資料。
`--verify-ui` 使用真實採樣、暫時設定和 AppKit 原生畫面擷取，會自動結束。
驗證結果與畫面在 `verification/`，詳細結果見 `verification/RESULTS.md`。
目前核心檢查 13/13、原生介面檢查 24/24 通過。滑鼠自動操作服務逾時，
實際拖移、選單與完整鍵盤互動仍需人工驗證。

## 設計與來源

`../design-system/design-system.json` 是唯一設計資料。`build.sh` 從它產生
資源快照，不能獨立編輯 `Resources/DesignTokens.json`。
動畫與字型由已核准的 Windows 資源複製；素材來源指紋在
`Resources/AssetProvenance.json`。`DESIGN.md` 與 `UX-CONTRACT.md` 記錄平台適配。

參考 [hagimi-monitor](https://github.com/Acerola-1/hagimi-monitor) 的採樣架構與
功耗範圍處理；本目錄未直接複製其 Swift 程式碼或視覺素材。低階 AppleSMC
讀取是依協定欄位另行實作，未加入寫入功能或私有 IOReport 相依。
專案授權沿用根目錄的 GPLv3。嵌入字型保留各自授權，來源見 `NOTICE.md`。
