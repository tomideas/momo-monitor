import { readFile, writeFile, rename, unlink } from 'node:fs/promises';
import { resolve, join } from 'node:path';
import { randomUUID } from 'node:crypto';
import { validate, designHash } from '../../design-system/design/server.mjs';
import { writeDesign } from '../../design-system/design/document.mjs';

const root = resolve(import.meta.dirname, '../..');
const directory = join(root, 'design-system');
const file = join(directory, 'design-system.json');
const summary = JSON.parse(await readFile(join(root, 'dev/verification/tray-menu/summary.json'), 'utf8'));
if (!summary.buildPassed || !summary.nativePassed) throw new Error('Verification incomplete');
const original = await readFile(file, 'utf8');
const data = validate(JSON.parse(original));
const preserved = JSON.stringify({ tokens: data.tokens, definitions: data.definitions, themes: data.themes, project: data.project });
const now = new Date().toISOString();
const evidence = '2026-10-07：Release 構建 0 warnings / 0 errors；自包含 Windows x64 publish 成功（NU1900 漏洞資訊端點無法讀取）。TrayStartupChecks 原生測試通過：隱藏啟動、系統匣可見、中英文選單標籤／順序／分隔線、Open Dashboard、Mini、由背景及 Mini 開啟 Settings、Exit 移除系統匣圖示。中英文原生選單截圖已目視核對。證據：dev/verification/tray-menu/ 及 dev/verification/tray-startup-checks.txt；未打包 Portable。' + (summary.rootUpdated ? '根目錄 MomoMonitor.exe 已替換並核對 SHA256。' : '新版位於 dev/artifacts/MomoMonitor-tray-menu.exe；根執行檔被執行中程式鎖住，尚待替換。');
let component = data.components.find(c => c.name === '系統匣選單');
if (!component) { component = { name: '系統匣選單', description: '' }; data.components.push(component); }
Object.assign(component, {
  description: '系統匣提供開啟儀表板、Mini、Settings 與 Exit，不顯示 Recent Alerts。選單文字使用一致的原生 Windows 字型和英文標題大小寫。',
  source: 'StatusMonitor/Services/TrayService.cs; StatusMonitor/MainWindow.Features.cs; StatusMonitor/I18n/Loc.cs',
  mapping: 'TrayService.Localize 共用 ContextMenuStrip 的原生字型，依序綁定 Loc[restore]、Loc[mini]、Loc[settings]、分隔線、Loc[exit]。英文為 Open Dashboard / Mini / Settings / Exit；繁中為開啟儀表板 / 迷你 / 設定 / 結束程式。MainWindow 的 Settings callback 呼叫 RestoreMainWindow 再 ShowSettingsPanel，沿用現有設定面板。BalloonTipClicked 仍導向指定提醒事件。',
  variants: 'English / 繁體中文；Windows 原生系統匣選單；主視窗、背景及 Mini 模式',
  states: '儀表板還原 / 開啟 Mini / 從背景開啟設定 / 從 Mini 開啟設定 / 退出',
  usage: '語言切換重建選單並釋放舊選單；Settings 先隱藏 Mini 並還原主視窗，再開啟共用設定面板。Exit 始終可用。提醒歷史仍由主頁提醒分頁與通知氣泡進入。',
  verification: evidence,
});
data.updatedAt = now;
data.updatedBy = 'Codex';
if (JSON.stringify({ tokens: data.tokens, definitions: data.definitions, themes: data.themes, project: data.project }) !== preserved) throw new Error('Unrelated design values changed');
data.application = { ...(data.application ?? {}), designRevision: designHash(data),
  files: ['StatusMonitor/Services/TrayService.cs', 'StatusMonitor/MainWindow.Features.cs', 'StatusMonitor/I18n/Loc.cs', 'StatusMonitor/MainWindow.FeatureChecks.cs'],
  verification: evidence, reportedBy: 'Codex', appliedAt: now };
validate(data);
if (await readFile(file, 'utf8') !== original) throw new Error('Design changed during merge');
const temporary = join(directory, '.design-system-' + randomUUID() + '.tmp');
try { await writeFile(temporary, JSON.stringify(data, null, 2) + '\n', { flag: 'wx' }); await rename(temporary, file); }
finally { await unlink(temporary).catch(() => {}); }
const checked = validate(JSON.parse(await readFile(file, 'utf8')));
if (checked.application.designRevision !== designHash(checked)) throw new Error('Design revision mismatch');
await writeDesign(directory, checked, designHash(checked), join(directory, 'design'));
console.log('Tray menu design reread and validated; unrelated values preserved; derived files generated.');
