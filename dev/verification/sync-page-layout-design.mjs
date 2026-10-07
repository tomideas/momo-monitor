import { readFile, writeFile, rename, unlink } from 'node:fs/promises';
import { resolve, join } from 'node:path';
import { randomUUID } from 'node:crypto';
import { validate, designHash } from '../../design-system/design/server.mjs';
import { writeDesign } from '../../design-system/design/document.mjs';

const root = resolve(import.meta.dirname, '../..');
const directory = join(root, 'design-system');
const file = join(directory, 'design-system.json');
const summary = JSON.parse(await readFile(join(root, 'dev/verification/dashboard-scroll/summary.json'), 'utf8'));
if (!summary.buildPassed || !summary.nativePassed) throw new Error('Verification incomplete');
const original = await readFile(file, 'utf8');
const data = validate(JSON.parse(original));
const preserved = JSON.stringify({ tokens: data.tokens, themes: data.themes, project: data.project });
const now = new Date().toISOString();
const evidence = '2026-10-07：Release 構建 0 warnings / 0 errors；PAPER POP / English 與 VOLT / 繁中各通過 161 項原生整合斷言及 4 項首頁捲動斷言（單層底部間距、放得下時不捲動、短視窗保留捲動、NET 可到達）。原生首頁、Info、Fans、Alerts 截圖已核對；WPF 產品目錄 strict 靜態審查零問題。Windows x64 自包含執行檔已準備，未打包 Portable。證據：dev/verification/dashboard-scroll/summary.json。';
function component(name, fields) {
  let entry = data.components.find(c => c.name === name);
  if (!entry) { entry = { name, description: '' }; data.components.push(entry); }
  Object.assign(entry, fields, { verification: evidence });
}
component('主分頁內容與捲動邊界', {
  description: '五個主分頁統一內容邊距；首頁只有一層底部留白，實際内容超出視窗才顯示捲動條。Info 不重複顯示頁面大標題或開場說明。',
  source: 'StatusMonitor/App.xaml; StatusMonitor/MainWindow.xaml; StatusMonitor/MainWindow.FeatureChecks.cs; DESIGN.md',
  mapping: 'App.xaml 的 PageContent StackPanel Style：Margin=12,4,12,12 DIP，供 DashboardView、InfoView、FansView、ProcessView、AlertsView 直接內容使用。StatBody 的 RowUniformPanel Margin=0,6,0,0；首頁 ContentControl 無尾端 Margin。感測狀態 StackPanel 僅顯示時提供上間距 12 DIP，不疊加底部間距。五頁 ScrollViewer 維持 VerticalScrollBarVisibility=Auto，沒有依賴硬編碼內容高度或強制隱藏捲動條。',
  states: '內容適合視窗 / 真正溢出 / 短視窗 / 實際狀態提示 / 電池資訊；PAPER POP / VOLT；繁中 / English',
  usage: '外層 PageContent 獨自提供頁尾 12 DIP；內層不得重複加尾端留白。保留資訊卡、風扇控制和提醒各自所需內容；頁面統一不移除必要功能。',
});
const info = data.components.find(c => c.name === 'Info 硬體資訊卡片網格');
component(info.name, {
  description: 'Info 直接顯示 Refresh 工具列與硬體資訊卡片，移除重複 Info 大標題、裝飾底線和開場說明。460 DIP 保留兩欄，寬視窗增加至三或四欄。',
  mapping: info.mapping + ' InfoView 內容使用共用 PageContent（12/4/12/12 DIP），Refresh 沿用 SecondaryButton、靠右、下間距 8 DIP；原 28 DIP 頁面標題、6 DIP 底線及 info_hint 不再顯示。',
});
const process = data.components.find(c => c.name === 'Process 分頁與主頁分工');
component(process.name, { mapping: process.mapping.replace('ProcessView 沿用 12/4/12/12 DIP 邊距', 'ProcessView 使用共用 PageContent 的 12/4/12/12 DIP 邊距') });
const window = data.components.find(c => c.name === '窗口尺寸下限与英雄区收缩');
component(window.name, { mapping: (window.mapping ?? '') + ' DashboardView 使用自動捲動；StatBody 和 ContentControl 移除多餘 16+16 DIP 尾端间距，由 PageContent 保留單層 12 DIP。VerifyDashboardScrollLayout 驗證實際 extent、viewport、scrollbar visibility 與 NET 可到達。' });
for (const [key, note] of [['--ds-space-2', 'PageContent 頂部間距 4 DIP'], ['--ds-space-7', 'PageContent 左右與底部間距 12 DIP；首頁狀態上間距 12 DIP']]) {
  if (data.definitions[key]) data.definitions[key].mapping += '; StatusMonitor/App.xaml / MainWindow.xaml: ' + note;
}
data.updatedAt = now;
data.updatedBy = 'Codex';
if (JSON.stringify({ tokens: data.tokens, themes: data.themes, project: data.project }) !== preserved) throw new Error('Unrelated design values changed');
data.application = { ...(data.application ?? {}), designRevision: designHash(data),
  files: ['StatusMonitor/MainWindow.xaml', 'StatusMonitor/App.xaml', 'StatusMonitor/MainWindow.FeatureChecks.cs', 'StatusMonitor/App.xaml.cs', 'DESIGN.md'],
  verification: evidence, reportedBy: 'Codex', appliedAt: now };
validate(data);
if (await readFile(file, 'utf8') !== original) throw new Error('Design changed during merge');
const temporary = join(directory, '.design-system-' + randomUUID() + '.tmp');
try { await writeFile(temporary, JSON.stringify(data, null, 2) + '\n', { flag: 'wx' }); await rename(temporary, file); }
finally { await unlink(temporary).catch(() => {}); }
const checked = validate(JSON.parse(await readFile(file, 'utf8')));
if (checked.application.designRevision !== designHash(checked)) throw new Error('Design revision mismatch');
await writeDesign(directory, checked, designHash(checked), join(directory, 'design'));
console.log('Page layout design merged, reread and validated; tokens and themes preserved.');
