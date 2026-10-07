import { readFile, writeFile, rename, unlink } from 'node:fs/promises';
import { resolve, join } from 'node:path';
import { randomUUID } from 'node:crypto';
import { validate, designHash } from '../../design-system/design/server.mjs';
import { writeDesign } from '../../design-system/design/document.mjs';

const root = resolve(import.meta.dirname, '../..');
const directory = join(root, 'design-system');
const file = join(directory, 'design-system.json');
const summary = JSON.parse(await readFile(join(root, 'dev/verification/fan-selector-remove/summary.json'), 'utf8'));
if (!summary.buildPassed || !summary.nativePassed) throw new Error('Verification incomplete');
const original = await readFile(file, 'utf8');
const data = validate(JSON.parse(original));
const preserved = JSON.stringify({ tokens: data.tokens, definitions: data.definitions, themes: data.themes, project: data.project });
const now = new Date().toISOString();
const evidence = '2026-10-07：Release 構建 0 warnings / 0 errors；48 項風扇 mock 斷言及 440 項功能斷言通過，涵蓋硬體自動來源、來源缺失不跨類別替代及舊 CPU／GPU／combined 設定忽略並保留名稱。PAPER POP / English 與 VOLT / 繁中各通過 159 項原生介面斷言，已核對自訂風扇畫面沒有 Follow／Auto／CPU／GPU 來源列，僅保留溫度曲線與固定轉速控制。Windows x64 自包含執行檔已準備，未打包 Portable；沒有真實風扇寫入。證據：dev/verification/fan-selector-remove/summary.json。';
const fan = data.components.find(c => c.name === '風扇頁（唯一會寫入硬體的頁）');
if (!fan) throw new Error('Missing fan component');
Object.assign(fan, {
  description: '風扇維持韌體 Auto，以及自訂溫度曲線或固定轉速。自訂區沒有 Follow／Auto／CPU／GPU 來源選項，溫度來源由風扇硬體自動決定。RPM、即時溫度與控制狀態沿用既有樣式。',
  source: 'StatusMonitor/MainWindow.xaml; StatusMonitor/ViewModels/FanProfileVm.cs; StatusMonitor/Models/FanFollow.cs; StatusMonitor/Settings/AppSettings.cs; StatusMonitor/Services/FanControlService.cs; StatusMonitor/I18n/Loc.cs',
  mapping: 'CustomPanel 僅以 SettingRadio 綁定 IsSensor／IsConstant；來源選擇 WrapPanel、IsSourceAuto／IsSourceCpu／IsSourceGpu／IsSourceMax、fan_source 系列文案、FanSource enum 與 FanIdentity.Source 均已移除。FanFollow.Category 僅接受 Target.Kind：GPU 風扇讀自身 GPU，其餘主機板／CPU 風扇讀 CPU。ReadLive／Apply 不讀使用者來源設定；ReadSource 不比較兩者、不跨類別補值。來源缺失沿用交回韌體及警告流程。',
  variants: '韌體 Auto / 自訂溫度曲線 / 自訂固定轉速；PAPER POP / VOLT；繁中 / English',
  usage: '取消整排來源選擇，不把 CPU／GPU 移到其他設定。GPU 風扇使用自己的顯示卡，主機板風扇使用 CPU；舊 JSON Source 欄位由既有反序列化規則忽略，不修改名稱、速度或曲線門檻。來源缺失不跨類別改讀，維持 Apply 套用流程及失敗交回韌體處理。',
  verification: evidence,
});
data.updatedAt = now;
data.updatedBy = 'Codex';
if (JSON.stringify({ tokens: data.tokens, definitions: data.definitions, themes: data.themes, project: data.project }) !== preserved) throw new Error('Unrelated design values changed');
data.application = { ...(data.application ?? {}), designRevision: designHash(data),
  files: ['StatusMonitor/MainWindow.xaml', 'StatusMonitor/ViewModels/FanProfileVm.cs', 'StatusMonitor/Models/FanFollow.cs', 'StatusMonitor/Settings/AppSettings.cs', 'StatusMonitor/Services/FanControlService.cs', 'StatusMonitor/I18n/Loc.cs', 'DESIGN.md'],
  verification: evidence, reportedBy: 'Codex', appliedAt: now };
validate(data);
if (await readFile(file, 'utf8') !== original) throw new Error('Design changed during merge');
const temporary = join(directory, '.design-system-' + randomUUID() + '.tmp');
try { await writeFile(temporary, JSON.stringify(data, null, 2) + '\n', { flag: 'wx' }); await rename(temporary, file); }
finally { await unlink(temporary).catch(() => {}); }
const checked = validate(JSON.parse(await readFile(file, 'utf8')));
if (checked.application.designRevision !== designHash(checked)) throw new Error('Design revision mismatch');
await writeDesign(directory, checked, designHash(checked), join(directory, 'design'));
console.log('Fan source design restored, reread and validated; unrelated design preserved.');
