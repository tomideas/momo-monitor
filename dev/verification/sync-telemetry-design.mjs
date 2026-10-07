import { readFile, writeFile, rename, unlink } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { randomUUID } from 'node:crypto';
import { validate, designHash } from '../../design-system/design/server.mjs';
import { writeDesign } from '../../design-system/design/document.mjs';

const root = resolve(import.meta.dirname, '../..');
const directory = join(root, 'design-system');
const file = join(directory, 'design-system.json');
const summary = JSON.parse(await readFile(join(root, 'dev/verification/telemetry-0.1.6/summary.json'), 'utf8'));
if (!summary.buildPassed || !summary.nativePassed || !summary.logicPassed) throw new Error('Verification incomplete');
const original = await readFile(file, 'utf8');
const data = validate(JSON.parse(original));
const preserved = JSON.stringify({ tokens: data.tokens, definitions: data.definitions, themes: data.themes, project: data.project });
const now = new Date().toISOString();
const evidence = `2026-10-07：0.1.6 Release 構建成功；${summary.checks}；原生 WPF PAPER POP / VOLT、繁中 / English 已驗證。根執行檔版本與感測庫指紋已核對；依使用者要求，預設建置只準備執行檔，Portable 打包須明確指定。證據位於 dev/verification/telemetry-0.1.6/。電池與跨機場景使用 mock，沒有筆電實機準確度驗證；未操作真實風扇寫入。`;
function component(name, fields) {
  let entry = data.components.find(item => item.name === name);
  if (!entry) { entry = { name, description: '' }; data.components.push(entry); }
  Object.assign(entry, fields, { verification: evidence });
}
component('感測詳情與報告', {
  description: 'CPU/GPU 追查保留來源名稱、功率範圍與本次監測的最小／最大讀數，提供使用者選擇位置匯出單次 CSV 感測報告。',
  source: 'StatusMonitor/MainWindow.xaml; StatusMonitor/MainWindow.Telemetry.cs; StatusMonitor/ViewModels/MainViewModel.Telemetry.cs; StatusMonitor/Sensors/SensorResolver.cs; StatusMonitor/Services/SensorReportService.cs',
  mapping: 'TrendsCard 的 SensorDetailsExpander / SensorDetails 使用共享 InkBrush、MutedBrush、HairBrush、Label、MonoFont 及 12/8/4 DIP 間距；SensorExportButton 使用 SecondaryButton 與原生 SaveFileDialog，成功／失敗顯示 SensorExportFeedback。來源名稱、ID、單位、量測範圍和 min/max 隨 Snapshot 保留。',
  states: '有讀值 / 無效與未回報 / 展開與收合 / 匯出成功與寫入失敗；PAPER POP / VOLT；繁中 / English',
  usage: 'CPU 溫度優先取有效 Package/Tctl/Tdie/CCD/Core，排除 Distance to TjMax 等限值。GPU 主溫度以 Core 為先，風扇可依有名稱的 Hot Spot；GPU Hot Spot / Memory 在感測詳情展示。不支援的來源維持缺值。CSV 僅在使用者操作後保存，不含主機名稱／序號或自動上傳。'
});
component('筆電電池監測', {
  description: '有系統電池時顯示電量、供電狀態以及獨立的充電／放電 W；有效放電率可作電池端功耗來源。',
  source: 'StatusMonitor/MainWindow.xaml; StatusMonitor/ViewModels/MainViewModel.Telemetry.cs; StatusMonitor/Sensors/BatterySensor.cs; StatusMonitor/Power/PowerAccounting.cs',
  mapping: 'BatteryTelemetry 綁定 HasBattery、BatterySummary、BatteryRateText，沿用 CardTitle / Label / 12 DIP 區塊間距。Windows Battery Status 以只讀 API 採樣，5 秒快取；resume/retry 失效。PowerLabel 隨 measured:battery 或 CPU+GPU 範圍切換。',
  states: '外接電源 / 電池供電 / 充電 / 放電 / idle / 多電池混合 / relative units / 無讀值',
  usage: '充電功率不等於電腦功耗。只有所有系統電池率值有效、絕對單位且方向一致才合計 W；UPS 不視為筆電。筆電或未知機型禁用桌機額定功率、主機板和 PSU 自動曲線。AC 時若 CPU/GPU 來源完整，只顯示清楚命名的 CPU+GPU 範圍，排除螢幕、周邊和變壓器損耗。'
});
component('功耗數字的來源標記', {
  description: 'CPU Package、整卡與 GPU 晶片分項分清來源，不將重疊功率域相加；估算以 ~ 標記，來源不完整使用破折號。',
  source: 'StatusMonitor/Power/PowerAccounting.cs; StatusMonitor/Power/PowerModel.cs; StatusMonitor/Sensors/SensorResolver.cs; StatusMonitor/ViewModels/MainViewModel.cs; StatusMonitor/ViewModels/MainViewModel.Telemetry.cs',
  mapping: 'PowerAccounting.Apply 統一總數範圍，PowerLabel / PowerNote / TotalWattsValue 與 HasPowerReading 同步；SensorDetails 保留 raw 功率分項與範圍。有效 0 W 不套用閒置曲線；CPU Package 包含核顯時不再累加核顯，核顯讀數仍可查看。',
  states: '量測 / 桌機模型估算 / 明確使用者校準 / 範圍不完整 / 有效零 / 電池放電 / AC CPU+GPU / 牆插估算',
  usage: '未知 CPU 不再假設 65 W，Laptop GPU 名稱不匹配桌機額定表。GPU 優先單一全卡來源，AMD Package 等 chip 來源保留展示，不能冒充整卡量測。桌機總數始終含周邊曲線，因此顯示估算；筆電和未知機型只用有效量測範圍或使用者明確輸入的校準。'
});
component('累計監測覆蓋與睡眠', {
  description: '能耗僅累計連續有效的監測區間，顯示監測時長、缺值時間、混合來源與舊紀錄限制。',
  source: 'StatusMonitor/Services/AwakeClock.cs; StatusMonitor/Power/EnergyIntegrator.cs; StatusMonitor/Services/EnergyHistory.cs; StatusMonitor/Services/MonitorService.cs; StatusMonitor/ViewModels/MainViewModel.PowerEvents.cs; StatusMonitor/ViewModels/MainViewModel.Telemetry.cs',
  mapping: 'QueryUnbiasedInterruptTime 排除睡眠；PowerModeChanged Suspend 交回韌體並重設基線，Resume 重開感測。EnergyIntegrator 梯形積分，按本地午夜分段；長缺口、未知值與範圍變更不補算。EnergyCoverageText 使用 Label 與 TextWrapping，days JSON 相容舊版本並新增 coverage metadata。',
  states: '首次基線 / 正常積分 / 有效零 / 缺值 / 長缺口 / 睡眠喚醒 / AC與電池來源切換 / 舊格式資料',
  usage: 'Today / 7 days / 30 days / All 仍是已記錄時段，不能當作未開啟程式時的完整每日用電。來源切換中斷功耗趨勢連線；舊累計保留，不臆造缺失監測秒數。'
});
const missing = data.components.find(c => c.name === '感測缺值與恢復說明');
if (missing) Object.assign(missing, {
  mapping: 'SensorStatusText / TrendReadingHelp / HasPowerReading 區分權限、驅動、未回報、延遲與裝置失敗；Hub.Update 逐設備樹隔離錯誤，All/Find 僅返回當下更新成功樹。Discovered 只保留身分。風扇控制拒絕失敗樹的舊溫度並嘗試交回韌體。',
  states: '可讀 / 部分設備失敗 / 權限受限 / 驅動缺失 / 未回報 / 重試恢復', verification: evidence
});
const fan = data.components.find(c => c.name === '風扇頁（唯一會寫入硬體的頁）');
if (fan) Object.assign(fan, { usage: fan.usage + ' 0.1.6：CPU/GPU 來源共用 SensorResolver；部分硬體失敗時保留其列但清空現值，healthy device 的監測可持續，失敗樹不可用於曲線。', verification: evidence });
const storage = data.components.find(c => c.name === '資料檔案位置（預設與可攜）');
if (storage) Object.assign(storage, {
  mapping: 'build.ps1 預設只建置 self-contained Windows x64 根執行檔；使用者明確要求打包時才指定 -Portable，產生 dist/MomoMonitor-Portable-win-x64/ 與 ZIP。AppSettings.DataDir 依程式旁 momo-data 存在性選擇資料位置；MonitoringTab 展示實際位置、模式與寫入狀態。正常建置不移動或覆蓋 momo-data。',
  verification: evidence
});
data.updatedAt = now;
data.updatedBy = 'Codex';
if (JSON.stringify({ tokens: data.tokens, definitions: data.definitions, themes: data.themes, project: data.project }) !== preserved)
  throw new Error('Unrelated design values changed');
data.application = { ...(data.application ?? {}), designRevision: designHash(data),
  files: ['StatusMonitor/MainWindow.xaml', 'StatusMonitor/MainWindow.Telemetry.cs', 'StatusMonitor/ViewModels/MainViewModel.Telemetry.cs', 'StatusMonitor/Sensors/SensorResolver.cs', 'StatusMonitor/Sensors/BatterySensor.cs', 'StatusMonitor/Power/PowerAccounting.cs', 'StatusMonitor/Power/EnergyIntegrator.cs', 'StatusMonitor/Services/EnergyHistory.cs'],
  verification: evidence, reportedBy: 'Codex', appliedAt: now };
validate(data);
if (await readFile(file, 'utf8') !== original) throw new Error('Design changed during merge');
const temporary = join(directory, '.design-system-' + randomUUID() + '.tmp');
try { await writeFile(temporary, JSON.stringify(data, null, 2) + '\n', { flag: 'wx' }); await rename(temporary, file); }
finally { await unlink(temporary).catch(() => {}); }
const checked = validate(JSON.parse(await readFile(file, 'utf8')));
if (checked.application.designRevision !== designHash(checked)) throw new Error('Design revision mismatch');
await writeDesign(directory, checked, designHash(checked), join(directory, 'design'));
console.log('Formal telemetry design reread and validated; tokens/themes preserved; derived files generated.');
