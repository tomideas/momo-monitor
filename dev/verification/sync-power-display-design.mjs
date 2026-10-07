import { readFile, writeFile, rename, unlink } from 'node:fs/promises';
import { resolve, join } from 'node:path';
import { randomUUID } from 'node:crypto';
import { validate, designHash } from '../../design-system/design/server.mjs';
import { writeDesign } from '../../design-system/design/document.mjs';

const root = resolve(import.meta.dirname, '../..');
const directory = join(root, 'design-system');
const file = join(directory, 'design-system.json');
const summary = JSON.parse(await readFile(join(root, 'dev/verification/power-display/summary.json'), 'utf8'));
if (!summary.buildPassed || !summary.nativePassed) throw new Error('Verification incomplete');
const original = await readFile(file, 'utf8');
const data = validate(JSON.parse(original));
const preserved = JSON.stringify({ tokens: data.tokens, definitions: data.definitions, themes: data.themes, project: data.project });
const component = data.components.find(c => c.name === '功耗數字的來源標記');
if (!component) throw new Error('Power display component missing');
const now = new Date().toISOString();
const evidence = '2026-10-07：Release 建置成功，0 warnings / 0 errors；自包含執行檔 publish 成功（NuGet 漏洞資訊端點因網路受限無法讀取）。PAPER POP / VOLT 各通過 152 項原生 WPF 互動檢查；繁中／English 設定說明與儀表板原生截圖已檢查。未更動功耗計算或估算來源 metadata；未打包 Portable。證據：dev/verification/power-display/。' + (summary.rootUpdated ? '根目錄執行檔已更新。' : '新版已備於 dev/artifacts/MomoMonitor-power-display.exe；根目錄執行檔仍被執行中的程式鎖住，尚未替換。');
Object.assign(component, {
  description: 'CPU Package、整卡與 GPU 晶片分項分清來源，不將重疊功率域相加；功耗數字不加近似符號，以「估算功耗」標題表達估算，計算方式只在設定說明。無有效讀值使用破折號。',
  source: [...new Set(component.source.split(';').map(s => s.trim()).concat(['StatusMonitor/App.xaml', 'StatusMonitor/MainWindow.xaml', 'StatusMonitor/I18n/Loc.cs']))].join('; '),
  variants: '零件功耗／牆插功耗以標題區分；首頁、迷你模式、CPU／GPU 瓦數都不加近似符號，不附曲線估算註腳。讀值不可用、電池來源或 CPU+GPU 範圍的必要說明仍顯示。設定「一般 → 用電與費率」提供簡短計算說明；未識別顯示卡校準與整機牆插功率設定沿用原有條件。',
  mapping: 'PowerAccounting.Apply 與 PowerEstimated metadata 沿用既有計算。MainViewModel.TotalWattsValue / CpuPowerText / BuildGpuCard.PowerText 使用純數值，Watts 不帶近似符號；PowerNote 不因 PowerEstimated 產生曲線註腳。App.xaml 的 StatBody 共用於首頁與迷你模式；MainWindow.xaml.PowerEstimateHelp 在一般設定綁定 Loc[power_estimate_help]，沿用 Label、TextWrapping 和 10 DIP 下間距。SensorDetails 保留 raw 來源和功率範圍。',
  verification: evidence,
});
data.updatedAt = now;
data.updatedBy = 'Codex';
if (JSON.stringify({ tokens: data.tokens, definitions: data.definitions, themes: data.themes, project: data.project }) !== preserved) throw new Error('Unrelated design changed');
data.application = { ...(data.application ?? {}), designRevision: designHash(data),
  files: ['StatusMonitor/ViewModels/MainViewModel.cs', 'StatusMonitor/MainWindow.xaml', 'StatusMonitor/App.xaml', 'StatusMonitor/I18n/Loc.cs'],
  verification: evidence, reportedBy: 'Codex', appliedAt: now };
validate(data);
if (await readFile(file, 'utf8') !== original) throw new Error('Design changed during merge');
const temporary = join(directory, '.design-system-' + randomUUID() + '.tmp');
try { await writeFile(temporary, JSON.stringify(data, null, 2) + '\n', { flag: 'wx' }); await rename(temporary, file); }
finally { await unlink(temporary).catch(() => {}); }
const checked = validate(JSON.parse(await readFile(file, 'utf8')));
if (checked.application.designRevision !== designHash(checked)) throw new Error('Design revision mismatch');
await writeDesign(directory, checked, designHash(checked), join(directory, 'design'));
console.log('Power display design reread and validated; unrelated values preserved; derived files generated.');
