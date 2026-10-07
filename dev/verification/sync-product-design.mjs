import { readFile, writeFile, rename, unlink } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { randomUUID } from 'node:crypto';
import { validate, designHash } from '../../design-system/design/server.mjs';
import { writeDesign } from '../../design-system/design/document.mjs';

const root = resolve(import.meta.dirname, '../..');
const directory = join(root, 'design-system');
const file = join(directory, 'design-system.json');
const original = await readFile(file, 'utf8');
const data = validate(JSON.parse(original));
const preserved = JSON.stringify({ tokens: data.tokens, definitions: data.definitions, themes: data.themes, project: data.project });
const now = new Date().toISOString();
function component(name, fields) {
  let entry = data.components.find(item => item.name === name);
  if (!entry) { entry = { name, description: '' }; data.components.push(entry); }
  Object.assign(entry, fields);
}
const evidence = '2026-10-07：Release 構建成功；FeatureChecks 431、AlertChecks 44、FanChecks 36、HubChecks 13 項通過；兩套主題原生 WPF 152 項互動斷言通過。460×760、460×330 與 1040 DIP 代表畫面位於 dev/verification/。風扇寫入與故障測試使用 mock，沒有操作實機風扇控制。';
component('風扇頁（唯一會寫入硬體的頁）', {
  description: '風扇列以 RPM 為主讀數，Auto、固定轉速與溫度曲線皆保留監測溫度、實際來源及控制狀態；自訂仍需確認後套用。',
  source: 'StatusMonitor/MainWindow.xaml; StatusMonitor/MainWindow.Features.cs; StatusMonitor/ViewModels/FanProfileVm.cs; StatusMonitor/Services/FanControlService.cs; StatusMonitor/Sensors/HardwareMonitorHub.cs',
  mapping: 'FansView / FanList 使用 SourceText、LiveText、ControlStatusText 與 ReadingStatusText。ReadLive 讀取現況與來源，Apply 決定控制模式；InvalidateReadings 清空故障讀值並嘗試交回韌體；RetryHardwareSensors 在採樣執行緒重開硬體。沿用 InkBrush、MutedBrush、AccentBrush、RowSub 與既有風扇 chip。',
  states: '韌體 Auto / 固定或曲線已接管 / 等待套用 / 溫度來源不可用 / 指令失敗 / 交回韌體失敗 / 僅監測；RPM 正值 / 回報 0 / 缺感測器 / 暫无讀值。沒有可控風扇時仍可進入頁面查看原因。',
  usage: '監測來源與曲線來源分開標示。CPU/GPU 風扇、缺值列與使用者設定的風扇保留可見；僅其他 Auto 通道的 0 RPM 預設隱藏，頁面顯示隱藏數量。溫度選擇先取有效值再按 Package/Core/Hot Spot 語義排序；GPU 控制只讀自己的顯示卡。完整更新失敗時不可使用舊溫度控制風扇。交回韌體失敗保留警告與待處理狀態，新硬體 handle 接受 SetDefault 後才標示成功。',
  verification: evidence + ' Fans 英文 PAPER POP、中文 VOLT 以及英文自訂面板已檢查；Auto GPU 列保留 34°C 與實際 GPU Core 來源。CPU Auto 的溫度監測由 mock 驗證；使用者 B850-I / RTX5090 的實際風扇寫入仍未驗證。'
});
component('資料檔案位置（預設與可攜）', {
  description: '正式 Portable 包自包含 Windows x64 執行環境，含空白 momo-data；設定、用電總計、每日歷史與最近七天提醒記錄存於程式旁。沒有 momo-data 時沿用 AppData。',
  source: 'build.ps1; StatusMonitor/Settings/AppSettings.cs; StatusMonitor/Settings/DataStorageService.cs; StatusMonitor/Services/EnergyHistory.cs; StatusMonitor/Services/AlertHistory.cs; StatusMonitor/ViewModels/MainViewModel.Storage.cs; StatusMonitor/MainWindow.xaml',
  mapping: 'build.ps1 預設 --self-contained true，發佈 dist/MomoMonitor-Portable-win-x64.zip。AppSettings.DataDir 依 Environment.ProcessPath 與 momo-data 存在性選擇一次；MonitoringTab 展示 StorageModeText、DataPath、StorageNotice、開啟資料夾與寫入檢查。TryWriteJson 寫入 .tmp 並原子替換，保留 .bak；ReadJsonWithRecovery 保留損毀原檔並回復備份。',
  variants: 'Portable 發佈包 / 舊 AppData 使用方式；正式需管理員的 manifest / 供驗證用的 asInvoker 測試包；PAPER POP / VOLT；繁中 / English',
  states: '空白資料夾首次匯入 / 已有可攜資料 / 正常儲存 / 唯讀位置 / 存檔失敗 / 備份恢復 / 無法恢復 / 換電腦後安全重設',
  usage: '搬移前退出程式並複製整個資料夾。發佈包不包含本機 settings、totals、energy-history 或 alert-history。唯讀位置不偷偷退回 AppData。換電腦或未驗證的舊設定保留偏好與歷史，將風扇模式回 Auto，重設硬體校正、啟動偏好與視窗位置。感測驅動與 Windows 自動啟動作用於目前電腦，安裝驅動仍需使用者確認。預覽不讀寫使用者歷史，也不接管風扇。',
  verification: evidence + ' 自包含測試 exe 直接啟動並完成 152 項原生互動斷言；Portable 設定畫面顯示 exe 旁路徑，預覽前後 momo-data 皆為空。ZIP 僅含 exe、LICENSE、雙語說明與空白 momo-data。純邏輯覆蓋原子備份、損毀恢復、避免以預設覆蓋壞檔與跨電腦重設。'
});
component('TrendChart 与 MetricBar', {
  source: 'StatusMonitor/Views/TrendChart.cs; StatusMonitor/Views/MetricBar.cs; StatusMonitor/ViewModels/MainViewModel.Features.cs; StatusMonitor/ViewModels/MainViewModel.Inspection.cs; StatusMonitor/MainWindow.xaml',
  mapping: 'TrendsCard 綁定 TrendPoints、TrendUnit、TrendSource 與 TrendReadingHelp；TrendGpuIndex 按被點選 GPU 的稳定 ID 選取歷史，缺值保留圖表空段。Inspect processes 與 Fans 導向相關頁面。繪圖仍由 TrendChart 與 MetricBar 的 C# 實作負責。',
  states: '即時 / 延遲 / 硬體讀數不可用 / 未回報溫度 / 未找到被選 GPU；60 秒 / 15 分鐘；僅本次開啟的歷史',
  verification: evidence + ' 驗證 RAM 趨勢、第二張 GPU 的來源與主要告警 GPU 偏好分離、提醒事件導向對應指標；本次沒有宣稱完成所有自繪控件狀態提取。'
});
component('首頁追查列與程序排序', {
  description: '首頁 CPU、GPU、RAM、DISK 整列可用滑鼠或原生按鈕鍵盤操作進入追查；保留原本用量條與資訊層級。',
  source: 'StatusMonitor/App.xaml; StatusMonitor/MainWindow.Inspection.cs; StatusMonitor/ViewModels/MainViewModel.Inspection.cs; StatusMonitor/Services/ProcessRanking.cs; StatusMonitor/Services/MonitorService.cs',
  mapping: '共享 InspectRowButton 延用 AccentFocusVisual、HoverBrush、InkBrush 與 6 DIP 圓角，無額外 Padding；CPU/RAM/DISK 綁定 InspectMetricCommand，GpuCardT 傳入具 ID 的 GPU。RevealInspection 同步趨勢与排序選擇器並捲動至 TrendsCard 或 ProcessesCard。ProcessRanking.Order 在完整樣本排序後才 Take。',
  variants: 'CPU 溫度 / 每張 GPU 溫度 / RAM 用量與記憶體程序 / DISK 活動程序；功耗、CPU、GPU、RAM、DISK 排序',
  states: '一般 / 懸停 / 鍵盤焦點 / 有讀值 / 缺值',
  usage: '點選第二張 GPU 不修改設定中的主要告警 GPU。程序表依所選問題排序完整樣本；不足額資料須沿用缺值與估算標記。點 CPU 或 GPU 後可直接前往風扇頁。',
  verification: evidence + ' 152 項 UI 斷言包含 RAM 追查、DISK 的真實共享按鈕綁定與排序選擇器、第二張 GPU 身分及偏好不變。'
});
component('最近提醒事件與處理', {
  description: '第四個「提醒 / Alerts」頁保留最近七天事件摘要，選取事件查看時間、持續長度、門檻、峰值及處理狀態。',
  source: 'StatusMonitor/MainWindow.xaml; StatusMonitor/MainWindow.Alerts.cs; StatusMonitor/ViewModels/MainViewModel.AlertEvents.cs; StatusMonitor/Services/AlertEngine.cs; StatusMonitor/Services/AlertHistory.cs; StatusMonitor/Services/TrayService.cs',
  mapping: 'AlertsView 使用原生 ListBox，InkBrush / CardBrush / HairBrush / HoverBrush 與 AccentFocusVisual；選取列左側 AccentBrush 邊線。SelectedAlertEvent 詳情使用共用 PrimaryButton / SecondaryButton；空清單和空選取不顯示無作用的控制項。TrayService BalloonTipClicked 依 EventId 開啟事件。AlertHistory 以 alert-history.json 持久存七天。',
  variants: 'CPU 溫度 / 主要 GPU 溫度及 VRAM / RAM / 磁碟剩餘容量；繁中 / English；PAPER POP / VOLT',
  states: '沒有事件 / 持續異常 / 已恢復 / 已中斷；未確認 / 已確認；缺讀值、取樣中斷、重啟、規則變更或關閉提醒的原因',
  usage: '確認代表使用者已看過事件，不代表恢復；已確認按鈕停用但異常狀態仍可持續。相關讀數導向對應 CPU/GPU/RAM 趨勢，磁碟事件導向資訊頁。歷史事件保留摘要，趨勢只涵蓋目前開啟期間。換視窗偏好不重設告警；變更監控 GPU、規則或啟用狀態才中斷相關事件。取樣頻率含閒置 5 秒以上間隔，時間缺口不可計為持续觀察。',
  verification: evidence + ' 原生中文事件詳情與英文 VOLT 空狀態已目視檢查；44 項告警斷言含取樣頻率、歷史恢復、確認語意、保留期限、摘要即時及語言反應、GPU 導航。示範事件只在 --render 內存使用。'
});
component('感測缺值與恢復說明', {
  description: '缺值使用破折號並在首頁、趨勢與風扇頁顯示可理解的原因及下一步；不能把未取得的資料當成零或正常。',
  source: 'StatusMonitor/ViewModels/MainViewModel.Inspection.cs; StatusMonitor/MainWindow.Inspection.cs; StatusMonitor/Services/SystemInfoService.cs; StatusMonitor/Sensors/HardwareMonitorHub.cs; StatusMonitor/Services/MonitorService.cs; StatusMonitor/I18n/Loc.cs',
  mapping: 'SensorStatusText / TrendReadingHelp 說明管理員權限、未安裝驅動、未回報感測器、讀數延遲或更新失敗；共用 SecondaryButton 提供重新讀取與監測設定入口。GetCpuStatic 在 WMI 拒絕存取時使用可取得的邏輯處理器數，不中斷啟動。Hub.Update 回傳完整更新結果，失敗時不曝露過期 sensor。',
  states: '可讀 / 權限受限 / 驅動缺失 / 未回報 / 更新失敗 / 重試後恢復',
  usage: 'OS 的 RAM、程序與網路資訊仍能在硬體感測失敗時更新；全域狀態說明硬體讀數不可用，不誤稱所有資料延遲。保留已知 GPU 身分而非假裝卡片消失。',
  verification: evidence + ' 13 項 HubChecks 模擬更新失敗、清除舊資料與重開；原生預覽在 WMI access denied 環境仍完成啟動及互動驗證。'
});
data.updatedAt = now;
data.updatedBy = 'Codex';
validate(data);
if (JSON.stringify({ tokens: data.tokens, definitions: data.definitions, themes: data.themes, project: data.project }) !== preserved)
  throw new Error('Unrelated design values changed');
data.application = {
  ...(data.application ?? {}),
  designRevision: designHash(data),
  files: ['StatusMonitor/App.xaml', 'StatusMonitor/MainWindow.xaml', 'StatusMonitor/MainWindow.Inspection.cs', 'StatusMonitor/MainWindow.Alerts.cs', 'StatusMonitor/ViewModels/MainViewModel.Inspection.cs', 'StatusMonitor/ViewModels/MainViewModel.AlertEvents.cs', 'StatusMonitor/ViewModels/FanProfileVm.cs', 'StatusMonitor/Services/FanControlService.cs', 'StatusMonitor/Services/AlertEngine.cs', 'StatusMonitor/Services/AlertHistory.cs', 'StatusMonitor/Settings/DataStorageService.cs', 'build.ps1'],
  verification: evidence + ' 僅記錄本次五項產品改善；無關設計值及原有未實作設計目標保持不變。',
  reportedBy: 'Codex', appliedAt: now
};
if (await readFile(file, 'utf8') !== original) throw new Error('Design changed during merge; reread before writing');
const temporary = join(directory, '.design-system-' + randomUUID() + '.tmp');
try { await writeFile(temporary, JSON.stringify(data, null, 2) + '\n', { flag: 'wx' }); await rename(temporary, file); }
finally { await unlink(temporary).catch(() => {}); }
const checked = validate(JSON.parse(await readFile(file, 'utf8')));
if (checked.application.designRevision !== designHash(checked)) throw new Error('Design revision mismatch');
await writeDesign(directory, checked, designHash(checked), join(directory, 'design'));
console.log('Formal design JSON reread and validated; shared components mapped; unrelated values preserved; derived documents generated.');
