import { readFile, writeFile, rename, unlink } from 'node:fs/promises';
import { resolve, join } from 'node:path';
import { randomUUID } from 'node:crypto';
import { validate, designHash } from '../../design-system/design/server.mjs';
import { writeDesign } from '../../design-system/design/document.mjs';

const root = resolve(import.meta.dirname, '../..');
const directory = join(root, 'design-system');
const file = join(directory, 'design-system.json');
const summary = JSON.parse(await readFile(join(root, 'dev/verification/settings-dialog/summary.json'), 'utf8'));
if (!summary.buildPassed || !summary.nativePassed || !summary.rootUpdated) throw new Error('Verification/deployment incomplete');
const original = await readFile(file, 'utf8');
const data = validate(JSON.parse(original));
const preserved = JSON.stringify({ tokens: data.tokens, definitions: data.definitions, themes: data.themes, project: data.project });
const now = new Date().toISOString();
const evidence = '2026-10-07：Release 構建 0 warnings / 0 errors；Windows x64 自包含 publish 成功（NU1900 漏洞資訊端點無法讀取）。PAPER POP / VOLT 各通過 161 項原生 WPF 整合斷言；設定 19 項原生斷言於繁中 PAPER POP、English VOLT 和 460×330 English 通過，包含草稿隔離、GPU 校準、取消／X、延後重置與啟動設定、OK 驗證及窄視窗捲動區。系統匣原生操作測試通過，首頁／Process／設定截圖已目視核對。根執行檔 2.22.32 已替換並核对 SHA256；未打包 Portable、未操作真實風扇控制或安裝驅動。證據：dev/verification/settings-dialog/summary.json。';
function component(name, fields) {
  let entry = data.components.find(c => c.name === name);
  if (!entry) { entry = { name, description: '' }; data.components.push(entry); }
  Object.assign(entry, fields, { verification: evidence });
}
component('设置输入与选择器', {
  description: '設定使用共用 WPF 輸入與選擇樣式，分為一般、監控與提醒、資料與說明三頁。右上 X 與最下方 OK／Cancel 統一確認及取消。',
  source: 'StatusMonitor/MainWindow.xaml; StatusMonitor/MainWindow.Settings.cs; StatusMonitor/MainWindow.xaml.cs; StatusMonitor/MainWindow.Features.cs; StatusMonitor/App.xaml; StatusMonitor/I18n/Loc.cs',
  mapping: 'SettingsTabs：General / Monitoring & alerts / Data & Info。SettingsXButton 沿用 IconButton；SettingsOkButton / SettingsCancelButton 沿用 PrimaryButton / SecondaryButton，最小寬 68 DIP、間距 8 DIP、最下方 SettingsFooter Grid.Row=3。SettingsHeader / SettingsFooter 一般下間距 12／上間距 18 DIP；視窗高度不足 480 DIP 時縮為 6／8 DIP，暫隱品牌與署名保留可捲動內容及確認鍵。PowerEstimateHelp 和 ResetTotalsButton 位於 DataInfoTab，沒有資料夾操作選項。',
  states: '開啟草稿 / 外觀預覽 / 取消與 X／Escape 還原 / 輸入無效停留 / OK 套用 / 重置待確認 / 460×330 短視窗',
  usage: 'BeginSettingsEdit 建立獨立 AppSettings 深複本，設定 handler 使用 EditedSettings。SaveEditedSettings / SaveFeatureSettings 不保存草稿或改變監測規則；啟動排程延至 OK。AcceptSettings 驗證目前文字與 GPU 校準 binding，只提交 DialogPreferences，不覆蓋視窗座標、風扇 profiles 或監測歷史。CancelSettingsEdit 還原語言、主題、字型、減少動效預覽。Reset totals 兩次確認後仍等待 OK，取消保留總計。'
});
const storage = data.components.find(c => c.name === '資料檔案位置（預設與可攜）');
component('資料檔案位置（預設與可攜）', {
  mapping: 'build.ps1 預設只準備 self-contained 根執行檔，-Portable 才打包。AppSettings.DataDir 保留程式旁 momo-data 或舊 AppData 的原有選擇規則。Settings 的 DataInfoTab 僅提供本機保存／搬移說明與實際保存異常，沒有路徑、開啟資料夾或寫入檢查按鈕。OpenDataInfo 從保存異常導向此頁；OpenMonitoring 從感測問題導向監控設定。正常建置和驗證不移動或覆蓋使用者 momo-data。',
  states: storage?.states ?? '正常保存 / 保存異常 / 本機說明 / 可攜搬移'
});
const power = data.components.find(c => c.name === '功耗數字的來源標記');
if (power) component(power.name, {
  mapping: power.mapping.replace('在一般設定', '在 DataInfoTab 資料與說明分頁'),
  variants: power.variants.replace('一般 → 用電與費率', '資料與說明 → 估算功耗'),
});
component('設計者署名頁尾', {
  description: '一般高度的設定頁尾保留品牌圖示、Designed by Tom Tam · tomideas.com · Version 2.22.32；最下方獨立 OK／Cancel。短視窗暫隱品牌與署名，優先保留設定内容和操作。',
  mapping: 'SettingsFooter：SettingsBrandRow 位於 Row=0；DesignerCredit 位於 Row=1、FontSize=10、MutedBrush、TextWrapping=Wrap、置中、上間距 12 DIP。DesignerWebsite 維持固定 HTTPS 網址、Bold 與 AccentBrush；版本從 assembly 的 CreditVersionText 取得。SettingsFeedback 位於 Row=2，OK／Cancel 位於 Row=3，ResetTotalsButton 已移入 DataInfoTab。UpdateSettingsLayout 於高度不足 480 DIP 暫隱品牌與署名，還原高度後再次顯示。',
});
component('Process 分頁與主頁分工', {
  description: '首頁只顯示功耗、CPU／GPU／RAM／DISK／NET 摘要與必要狀態；Process 分頁集中 Trends 和 Top Processes，Fans & Pump 摘要不再重複顯示。',
  source: 'StatusMonitor/MainWindow.xaml; StatusMonitor/MainWindow.xaml.cs; StatusMonitor/MainWindow.Inspection.cs; StatusMonitor/ViewModels/MainViewModel.Inspection.cs; StatusMonitor/I18n/Loc.cs',
  mapping: 'NavProcess 綁定 Loc[nav_process]，English 為 Process、繁中為程序。Page.Process / SelectView 統一控制 ProcessView 和導覽選取。ProcessView 使用既有 12/4/12/12 DIP 頁面邊距，持有 TrendsCard、SensorDetailsExpander、ProcessesCard 與 ProcessSortCombo；首頁移除原有 FansText 區塊。SetPagesEnabled 同時處理五頁的 modal 停用／還原。',
  variants: 'Dashboard / Info / Fans / Process / Alerts；繁中 / English；PAPER POP / VOLT；460 DIP 最小寬',
  states: '首頁摘要 / Process 趨勢與程序 / 每個硬體的追查 / 告警導向 / 缺值 / 程序排序',
  usage: '首頁 CPU／GPU／RAM／DISK 的原生整列按鈕仍可追查。RevealInspection 切到 Process，溫度展開 Sensor details，磁碟活動捲到 ProcessesCard；程序依所選問題排序。InspectProcesses 同頁捲動，InspectFans 仍導向 Fans。提醒歷史與主要 GPU 偏好保留原有行為。',
});
const inspection = data.components.find(c => c.name === '首頁追查列與程序排序');
if (inspection) component(inspection.name, {
  description: '首頁 CPU、GPU、RAM、DISK 整列保留原生按鈕追查，跳至 Process 分頁的對應趨勢與程序排序。',
  mapping: 'InspectRowButton / InspectMetricCommand 延用既有焦點、懸停及 GPU ID；RevealInspection 導向 Page.Process，設定 TrendMetricCombo 與 ProcessSortCombo，再捲至 TrendsCard 或 ProcessesCard。ProcessRanking.Order 在完整樣本排序後才 Take；第二張 GPU 身分不改變告警偏好。',
});
const trends = data.components.find(c => c.name === 'TrendChart 与 MetricBar');
if (trends) component(trends.name, { mapping: trends.mapping + ' TrendsCard 現在位於 ProcessView；MetricBar 仍用於首頁與 Info。' });
const alerts = data.components.find(c => c.name === '最近提醒事件與處理');
if (alerts) component(alerts.name, {
  description: '提醒 / Alerts 分頁保留最近七天事件摘要，選取事件查看時間、持續長度、門檻、峰值及處理狀態。',
  mapping: alerts.mapping + ' 溫度、RAM 與 GPU 事件追查經 RevealInspection 導向 Process，磁碟事件仍導向 Info。',
});
const tray = data.components.find(c => c.name === '系統匣選單');
if (tray) component(tray.name, {});
data.updatedAt = now;
data.updatedBy = 'Codex';
if (JSON.stringify({ tokens: data.tokens, definitions: data.definitions, themes: data.themes, project: data.project }) !== preserved) throw new Error('Unrelated design values changed');
data.application = { ...(data.application ?? {}), designRevision: designHash(data),
  files: ['StatusMonitor/MainWindow.xaml', 'StatusMonitor/MainWindow.xaml.cs', 'StatusMonitor/MainWindow.Settings.cs', 'StatusMonitor/MainWindow.Features.cs', 'StatusMonitor/MainWindow.Inspection.cs', 'StatusMonitor/I18n/Loc.cs', 'StatusMonitor/App.xaml.cs'],
  verification: evidence, reportedBy: 'Codex', appliedAt: now };
validate(data);
if (await readFile(file, 'utf8') !== original) throw new Error('Design changed during merge');
const temporary = join(directory, '.design-system-' + randomUUID() + '.tmp');
try { await writeFile(temporary, JSON.stringify(data, null, 2) + '\n', { flag: 'wx' }); await rename(temporary, file); }
finally { await unlink(temporary).catch(() => {}); }
const checked = validate(JSON.parse(await readFile(file, 'utf8')));
if (checked.application.designRevision !== designHash(checked)) throw new Error('Design revision mismatch');
await writeDesign(directory, checked, designHash(checked), join(directory, 'design'));
console.log('Settings and Process design reread and validated; unrelated values preserved; derived files generated.');
