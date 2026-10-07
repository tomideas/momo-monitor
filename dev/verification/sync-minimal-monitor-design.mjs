import { readFile, writeFile, rename, unlink } from 'node:fs/promises';
import { resolve, join } from 'node:path';
import { randomUUID } from 'node:crypto';
import { validate, designHash } from '../../design-system/design/server.mjs';
import { writeDesign } from '../../design-system/design/document.mjs';

const root = resolve(import.meta.dirname, '../..');
const directory = join(root, 'design-system');
const file = join(directory, 'design-system.json');
const summary = JSON.parse(await readFile(join(root, 'dev/verification/minimal-monitor/summary.json'), 'utf8'));
if (!summary.buildPassed || !summary.nativePassed) throw new Error('Verification incomplete');
const original = await readFile(file, 'utf8');
const data = validate(JSON.parse(original));
const preserved = JSON.stringify({ tokens: data.tokens, definitions: data.definitions, themes: data.themes, project: data.project });
const now = new Date().toISOString();
const evidence = '2026-10-07：Release 構建 0 warnings / 0 errors；PAPER POP / English 和 VOLT / 繁中各通過 161 項原生 WPF 整合斷言；電池充電與放電各通過 9 項原生資料斷言。首頁／Process 460×760 DIP 原生截圖已目視核對。Windows x64 自包含執行檔已準備；未打包 Portable。證據：dev/verification/minimal-monitor/summary.json。';
function component(name, fields) {
  const entry = data.components.find(c => c.name === name);
  if (!entry) throw new Error('Missing component: ' + name);
  Object.assign(entry, fields, { verification: evidence });
}
component('Process 分頁與主頁分工', {
  description: '首頁保留功耗及硬體摘要，沒有底部操作提示；Process 分頁僅集中趨勢與主要程序，不顯示感測詳情、附加操作按鈕或監測覆蓋文案。',
  source: 'StatusMonitor/MainWindow.xaml; StatusMonitor/MainWindow.xaml.cs; StatusMonitor/MainWindow.Inspection.cs; StatusMonitor/I18n/Loc.cs',
  mapping: 'Page.Process / SelectView 控制 ProcessView 和 NavProcess。ProcessView 沿用 12/4/12/12 DIP 邊距、SectionRule、TrendsCard 和 ProcessesCard；保留 TrendMetricCombo、60 s／15 min、TrendSource、TrendChart、TrendSummary、ReadingStatus 和 ProcessSortCombo。沒有 InspectionHint、TrendReadingHelp、SensorDetailsExpander、SensorExportButton、SensorExportFeedback 或 EnergyCoverageText 的顯示元素。',
  usage: '首頁 CPU／GPU／RAM 點選後經 RevealInspection 導向對應趨勢。DISK 只顯示容量與用量，不提供按鈕、懸停或鍵盤操作。告警追查、主要 GPU 偏好和程序排序保留。風扇從 Fans 分頁查看。',
});
component('首頁追查列與程序排序', {
  description: '首頁 CPU、GPU、RAM 整列可追查到 Process；DISK 是靜態摘要。首頁不顯示操作教學文案。',
  mapping: 'App.xaml StatBody 的 CPU／GPU／RAM 使用 InspectRowButton / InspectMetricCommand；DISK 直接使用 LoadRow Border 與 Grid，保留 DiskRows、MetricBar、百分比和容量。RevealInspection 切至 Process 並同步 TrendMetricCombo／ProcessSortCombo。ProcessRanking.Order 仍對完整樣本排序後才 Take。',
  variants: 'CPU 溫度 / 每張 GPU 溫度 / RAM 用量；程序可依功耗、CPU、GPU、RAM、DISK 排序；DISK 靜態容量摘要',
  states: 'CPU／GPU／RAM：一般 / 懸停 / 鍵盤焦點；DISK：純顯示；有讀值 / 缺值',
  usage: '第二張 GPU 的追查不修改主要告警 GPU。DISK 不可點擊，磁碟活動仍可由 Process 的排序選擇器查看。',
});
component('感測詳情與報告', {
  description: '底層保留感測來源、量測範圍、最小／最大讀數及報告服務；Process 不提供感測詳情或匯出入口。',
  source: 'StatusMonitor/MainWindow.Telemetry.cs; StatusMonitor/ViewModels/MainViewModel.Telemetry.cs; StatusMonitor/Sensors/SensorResolver.cs; StatusMonitor/Services/SensorReportService.cs',
  mapping: 'Snapshot 和 MainViewModel.Telemetry 保留 SensorDetails、TemperatureSourceText、PowerSourceText 和 ExportSensorReport 資料能力；SensorReportService 保留 CSV 產生服務。產品 UI 已移除 SensorDetailsExpander、SensorExportButton、SensorExportFeedback 與原生 SaveFileDialog handler，沒有感測報告操作入口。',
  states: '底層有效 / 無效 / 未回報讀數；來源與 min/max metadata；不在 Process 展示',
  usage: 'CPU／GPU 溫度選取規則不變；Hot Spot／Memory 等來源保留在底層資料，沒有新增介面文案或自動匯出／上傳。',
});
component('累計監測覆蓋與睡眠', {
  description: '能耗僅累計連續有效的監測區間，保留覆蓋時間與來源 metadata；Process 不顯示監測時長說明。',
  mapping: 'AwakeClock 的 QueryUnbiasedInterruptTime 排除睡眠；PowerModeChanged Suspend／Resume 處理感測基線。EnergyIntegrator 按本地午夜分段，長缺口、未知值與範圍變更不補算；days JSON 的 coverage metadata 及 EnergyCoverageText 資料計算保留，MainWindow.xaml 不顯示此文案。',
});
component('TrendChart 与 MetricBar', {
  mapping: 'ProcessView 的 TrendsCard 綁定 TrendPoints、TrendUnit、TrendSource、TrendSummary、ReadingStatus；保留指標選擇與 60 秒／15 分鐘切換。TrendGpuIndex 使用被點選 GPU 的穩定 ID，缺值保留空段。移除 TrendReadingHelp、感測詳情和附加操作列。TrendChart／MetricBar 仍由 C# 控件繪製，MetricBar 用於首頁與 Info。',
});
data.updatedAt = now;
data.updatedBy = 'Codex';
if (JSON.stringify({ tokens: data.tokens, definitions: data.definitions, themes: data.themes, project: data.project }) !== preserved) throw new Error('Unrelated design values changed');
data.application = { ...(data.application ?? {}), designRevision: designHash(data),
  files: ['StatusMonitor/MainWindow.xaml', 'StatusMonitor/App.xaml', 'StatusMonitor/MainWindow.Inspection.cs', 'StatusMonitor/MainWindow.Telemetry.cs', 'StatusMonitor/App.xaml.cs'],
  verification: evidence, reportedBy: 'Codex', appliedAt: now };
validate(data);
if (await readFile(file, 'utf8') !== original) throw new Error('Design changed during merge');
const temporary = join(directory, '.design-system-' + randomUUID() + '.tmp');
try { await writeFile(temporary, JSON.stringify(data, null, 2) + '\n', { flag: 'wx' }); await rename(temporary, file); }
finally { await unlink(temporary).catch(() => {}); }
const checked = validate(JSON.parse(await readFile(file, 'utf8')));
if (checked.application.designRevision !== designHash(checked)) throw new Error('Design revision mismatch');
await writeDesign(directory, checked, designHash(checked), join(directory, 'design'));
console.log('Minimal monitor design merged, reread and validated; shared tokens preserved.');
