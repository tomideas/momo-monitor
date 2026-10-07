import { readFile, writeFile, rename, unlink } from 'node:fs/promises';
import { resolve, join } from 'node:path';
import { createHash, randomUUID } from 'node:crypto';
import { validate, designHash } from '../../design-system/design/server.mjs';
import { writeDesign } from '../../design-system/design/document.mjs';

const root = resolve(import.meta.dirname, '../..');
const directory = join(root, 'design-system');
const evidenceDirectory = join(root, 'dev/verification/design-system-current');
const file = join(directory, 'design-system.json');
const original = await readFile(file, 'utf8');
const data = validate(JSON.parse(original));
const before = structuredClone(data);
const theme = await readFile(join(root, 'StatusMonitor/Views/Theme.cs'), 'utf8');
const app = await readFile(join(root, 'StatusMonitor/App.xaml'), 'utf8');
const main = await readFile(join(root, 'StatusMonitor/MainWindow.xaml'), 'utf8');
const mini = await readFile(join(root, 'StatusMonitor/MiniWindow.xaml'), 'utf8');
const splash = await readFile(join(root, 'StatusMonitor/SplashWindow.xaml'), 'utf8');
const baseline = JSON.parse(await readFile(join(directory, 'design/guidelines/import-baseline.json'), 'utf8'));
const now = new Date().toISOString();
const verified = '2026-10-07：按當前 WPF 源碼反向同步，未修改產品源碼或根執行檔。隔離副本 Release 構建 0 warnings / 0 errors；PAPER POP / English 與 VOLT / 繁中各通過 159 項原生介面、4 項首頁捲動、19 項設定斷言。首頁、Info、Process、Fans 自訂、設定與 Mini 原生截圖已核對；字體、顏色、形狀和間距以目前 runtime Style／Theme.Apply 為準。證據：dev/verification/design-system-current/。';
const actualTokens = new Set();
function definition(key, source, mapping, description) {
  Object.assign(data.definitions[key], { source, mapping });
  if (description) data.definitions[key].description = description;
}
function token(key, value, source, mapping, description) {
  if (!(key in data.tokens)) throw new Error('Unknown token: ' + key);
  data.tokens[key] = value;
  actualTokens.add(key);
  definition(key, source, mapping, description);
}
const sourceTheme = 'StatusMonitor/Views/Theme.cs（目前 Theme.Apply 實值，2026-10-07）';
const css = value => value.length === 9 ? '#' + value.slice(3) + value.slice(1, 3) : value;
function palette(dark) {
  const values = {};
  for (const match of theme.matchAll(/string (\w+) = dark \? "(#[0-9A-F]+)" : "(#[0-9A-F]+)";/g))
    values[match[1]] = css(match[dark ? 2 : 3]);
  return values;
}
const roles = {
  '--ds-accent': ['pop', 'AccentBrush／AmberBrush'],
  '--ds-bg-page': ['ground', 'PaperBrush／PopPaper 底色'],
  '--ds-bg-surface': ['raise', 'CardBrush：輸入框、次要按鈕與 Info 卡片'],
  '--ds-bg-elevated': ['raise', 'CardBrush：ComboBox Popup 與 ToolTip 浮層；設定／Mini 的變體使用 PopPaper'],
  '--ds-bg-pattern': ['dot', 'DotPaper：20 DIP 方格內半徑 0.8 DIP 圓點'],
  '--ds-border': ['line', 'HairBrush／TrackBrush'],
  '--ds-border-subtle': ['line', 'HairBrush：SectionRule、行分隔、表格網格，共用邊線色'],
  '--ds-border-strong': ['ink', 'InkBrush：Info 類別徽章及權限提示的 1.5 DIP 強邊界'],
  '--ds-border-focus': ['pop', 'AccentBrush：TextBox 焦點、AccentFocusVisual 與 Mini 角色鍵盤框'],
  '--ds-text-primary': ['ink', 'InkBrush'],
  '--ds-text-secondary': ['muted', 'MutedBrush：Label／RowSub'],
  '--ds-text-tertiary': ['muted', 'MutedBrush：MicroLabel／RowName／DesignerCredit 元資料，共用次級色'],
  '--ds-text-inverse': ['onPop', 'OnAccentBrush：強調填色上的文字與勾選圖形，與 on-accent 共用'],
  '--ds-on-accent': ['onPop', 'OnAccentBrush'],
  '--ds-text-link': ['pop', 'AccentBrush：DesignerWebsite Hyperlink、Bold，固定 HTTPS 網址'],
  '--ds-interactive-hover': ['hover', 'HoverBrush：ChipTemplate、NavButton、列表列與 Mini 圖示鍵'],
  '--ds-interactive-pressed': ['hover', 'ChipTemplate 滑鼠按下沿用 HoverBrush；另以 TranslateTransform 2/2 DIP 表達按下'],
  '--ds-accent-pressed': ['pop', 'SolidChipTemplate 按下保留 AccentBrush；另有 2/2 DIP 位移，不換独立色'],
  '--ds-danger': ['alarm', 'AlarmBrush／ToneBrushConverter 超標色'],
};
const paper = palette(false), volt = palette(true);
for (const [key, [variable, mapping]] of Object.entries(roles)) {
  if (!paper[variable] || !volt[variable]) throw new Error('Missing runtime palette: ' + variable);
  token(key, paper[variable], sourceTheme + '; StatusMonitor/App.xaml; StatusMonitor/MainWindow.xaml', mapping);
  for (const name of Object.keys(data.themes)) {
    if (name !== 'VOLT') throw new Error('Unmapped runtime theme: ' + name);
    if (volt[variable] === paper[variable]) delete data.themes[name][key];
    else data.themes[name][key] = volt[variable];
  }
}
const solidOpacity = main.match(/TargetName="bd" Property="Opacity" Value="([\d.]+)"/)[1];
function rgba(hex, alpha) { return `rgba(${[1, 3, 5].map(i => parseInt(hex.slice(i, i + 2), 16)).join(',')},${alpha})`; }
token('--ds-accent-hover', rgba(paper.pop, solidOpacity), 'StatusMonitor/MainWindow.xaml SolidChipTemplate；Theme.cs', '悬停背景仍是 AccentBrush，bd 整面 Opacity=' + solidOpacity + '，含文字一起變淡；token 記錄填色透明度，非獨立換色。');
for (const name of Object.keys(data.themes)) data.themes[name]['--ds-accent-hover'] = rgba(volt.pop, solidOpacity);
token('--ds-interactive', '#00000000', 'StatusMonitor/MainWindow.xaml IconButton／NavButton；App.xaml PeriodChip', '中性图示、導覽和未選時段的默认背景 Transparent；SecondaryButton 的 CardBrush 是獨立變體。');
token('--ds-bg-overlay', '#00000066', 'StatusMonitor/MainWindow.xaml Overlay Grids', 'DetailsOverlay／CloseOverlay／DriverOverlay／SettingsOverlay 使用 WPF #66000000；轉換為 CSS #00000066。');
const warning = main.match(/<Border Background="(#[0-9A-F]+)"[^>]+HasWarning[\s\S]+?Foreground="(#[0-9A-F]+)"/);
if (!warning) throw new Error('Warning surface not found');
token('--ds-warning-subtle', warning[1], 'StatusMonitor/MainWindow.xaml HasWarning Border', '權限提示背景直接使用此色；不是未實作的假值。');
token('--ds-warning', warning[2], 'StatusMonitor/MainWindow.xaml admin_required TextBlock', '權限提示文字直接使用此色；固定在淺黃提示面上，兩套皮膚共用。');
for (const name of Object.keys(data.themes)) for (const key of ['--ds-interactive', '--ds-bg-overlay', '--ds-warning', '--ds-warning-subtle']) delete data.themes[name][key];

function styleAttribute(source, key, property) {
  const start = source.indexOf('<Style x:Key="' + key + '"');
  if (start < 0) throw new Error('Missing style: ' + key);
  const block = source.slice(start, source.indexOf('</Style>', start));
  const match = block.match(new RegExp('Property="' + property + '" Value="([^"]+)"'));
  if (!match) throw new Error('Missing style property: ' + key + '/' + property);
  return match[1];
}
const settingsHeading = main.match(/Path=\[settings\][\s\S]{0,160}?FontSize="(\d+)"/);
if (!settingsHeading) throw new Error('Settings heading not found');
token('--ds-display-size', settingsHeading[1] + 'px', 'StatusMonitor/MainWindow.xaml SettingsHeader', '現行設定主標題 ' + settingsHeading[1] + ' DIP；Info 大標題已移除。Hero、RPM 和品牌讀數是另外的顯示層。');
token('--ds-display-weight', '700', 'StatusMonitor/MainWindow.xaml SettingsHeader', 'Settings 主標題 FontWeight=Bold（700）。');
token('--ds-title-size', '17px', 'StatusMonitor/MainWindow.xaml section／dialog TextBlocks', '設定分區、提醒詳情與關閉／驅動確認標題 FontSize=17 DIP；CardTitle 使用13 DIP。');
token('--ds-title-weight', '700', 'StatusMonitor/MainWindow.xaml section／dialog TextBlocks', '17 DIP 分區和確認標題 FontWeight=Bold。');
token('--ds-font-size', main.match(/FontSize="(\d+)" WindowStartupLocation/)[1] + 'px', 'StatusMonitor/MainWindow.xaml Window；MiniWindow.xaml；SplashWindow.xaml', '預設 Window 字號12 DIP，不是每個角色的字号。');
token('--ds-label-size', styleAttribute(main, 'Label', 'FontSize') + 'px', 'StatusMonitor/MainWindow.xaml Label；App.xaml RowTag', 'Label／SettingRow／RowTag 12 DIP；字重依元件決定。');
token('--ds-micro-size', styleAttribute(app, 'MicroLabel', 'FontSize') + 'px', 'StatusMonitor/App.xaml MicroLabel／RowName', 'MicroLabel／RowName 10 DIP；RowSub 独立為11 DIP、Medium。');
token('--ds-font-weight', '700', 'StatusMonitor/App.xaml MicroLabel', 'MicroLabel 使用 Bold（700）；Label 正常字重、RowTag Black、RowSub Medium，不把全部正文改成粗體。');
token('--ds-font-ui', '"Geist", "Noto Sans TC", sans-serif', 'StatusMonitor/App.xaml AppFontFamily；StatusMonitor/Settings/AppFont.cs', '預設內嵌 Geist／Noto Sans TC；AppFont.Resolve 的使用者字型選擇覆蓋此預設。');
token('--ds-font-mono', '"Geist Mono", "Noto Sans TC", monospace', 'StatusMonitor/App.xaml MonoFont／RowName／RowSub', '內嵌 Geist Mono／Noto Sans TC 用於硬體名稱、副讀數、趨勢摘要和版本等數值資訊。');
const sourceFiles = { 'StatusMonitor/App.xaml': app, 'StatusMonitor/MainWindow.xaml': main, 'StatusMonitor/MiniWindow.xaml': mini, 'StatusMonitor/SplashWindow.xaml': splash };
for (let i = 1; i <= 10; i++) {
  const key = '--ds-space-' + i;
  const value = Number(data.tokens[key].replace('px', ''));
  const matches = [];
  for (const [path, source] of Object.entries(sourceFiles)) for (const m of source.matchAll(/(?:Margin|Padding)="([\d.,-]+)"/g))
    if (m[1].split(',').map(Number).includes(value)) matches.push(path + ': ' + m[0]);
  if (!matches.length) throw new Error('No actual spacing for ' + key);
  token(key, value + 'px', matches[0].split(': ')[0] + '（当前源码实测）', [...new Set(matches)].slice(0, 4).join('; ') + '。WPF 單位為 DIP，保留角色差異，不以過期行號或出現次數作 mapping。');
}
definition('--ds-space-7', 'StatusMonitor/App.xaml PageContent；MainWindow.xaml', 'PageContent Margin=12,4,12,12 DIP；五個主分頁共用。首頁正常底部只保留這層12 DIP，StatBody 与 host 無尾端間距。', '頁面左右與底部共同間距、設定欄位分隔。');
definition('--ds-space-9', 'StatusMonitor/MainWindow.xaml 主導航 Grid；App.xaml hero summary', '主導航 Margin=18,16,18,10；hero 摘要左間距18 DIP。主內容使用 PageContent 的12 DIP，不是18 DIP。', '主導航邊距與英雄區摘要分隔。');
for (const [key, symbol, value] of [['--ds-radius-xs', 'Card／导航下划线／QuietScrollThumb', 2], ['--ds-radius-sm', 'NavChipTemplate／Info 分类徽章／MiniIconButton', 5], ['--ds-radius-md', 'ChipTemplate／SolidChipTemplate／TextBox／ComboBox', 6], ['--ds-radius-lg', '权限提示／Mini Border', 8], ['--ds-radius-xl', 'Settings／Close／Driver／Splash Border', 12]]) {
  const present = Object.values(sourceFiles).some(s => s.includes('CornerRadius="' + value + '"') || s.includes('Property="CornerRadius" Value="' + value + '"'));
  if (!present) throw new Error('Missing radius: ' + value);
  token(key, value + 'px', 'StatusMonitor/App.xaml; MainWindow.xaml; MiniWindow.xaml; SplashWindow.xaml', symbol + '：CornerRadius=' + value + ' DIP。复选框和时段芯片另有4 DIP例外，不使用藥丸。');
}
for (const [key, source, path] of [['--ds-shadow-sm', mini, 'StatusMonitor/MiniWindow.xaml MiniShadow'], ['--ds-shadow-md', splash, 'StatusMonitor/SplashWindow.xaml Border.Effect'], ['--ds-shadow-lg', main.slice(main.indexOf('x:Name="CloseOverlay"')), 'StatusMonitor/MainWindow.xaml Close／Driver／Settings Border.Effect']]) {
  const match = source.match(/<DropShadowEffect[^>]+BlurRadius="([\d.]+)"[^>]+ShadowDepth="([\d.]+)"[^>]+Direction="270"[^>]+Color="#000000"[^>]+Opacity="([\d.]+)"/);
  if (!match) throw new Error('Missing shadow: ' + key);
  token(key, `0 ${match[2]}px ${match[1]}px rgba(0,0,0,${match[3]})`, path, `WPF BlurRadius=${match[1]}、ShadowDepth=${match[2]}、Direction=270、Opacity=${match[3]}；CSS 為向下偏移。`);
}
for (const key of Object.keys(data.tokens)) if (!actualTokens.has(key)) {
  data.definitions[key].source = '未來設計目標（目前沒有獨立 runtime 語義位置；2026-10-07 核對）';
  data.definitions[key].mapping = data.definitions[key].mapping.replace(/App\.xaml:\d+(?:-\d+)?|MainWindow\.xaml:\d+(?:-\d+)?|MiniWindow\.xaml:\d+(?:-\d+)?/g, m => m.split(':')[0]);
}
function component(name, fields) { const c = data.components.find(c => c.name === name); if (!c) throw new Error('Missing component: ' + name); Object.assign(c, fields, { verification: verified }); }
function guideline(name, fields) { const g = data.guidelines.find(g => g.name === name); if (!g) throw new Error('Missing guideline: ' + name); Object.assign(g, fields); }
component('MetricCard', { description: 'Card／MetricCard 是現存共享樣式；首頁實際使用 LoadRow 監測列，Info 使用 Card 的280 DIP卡片。MetricCard 的220 DIP最小高度樣式目前未被產品畫面引用。', mapping: 'MainWindow.xaml Card：CardBrush、HairBrush、BorderThickness=1、CornerRadius=2、Padding=16。MetricCard 基於Card，保留MinHeight=220與Window.Tag=True時設0的舊變體；不冒充目前首頁呈現。', usage: '首頁以 App.xaml StatBody 的 LoadRow 呈現；Mini使用自己的MiniValue／MiniDetail布局。' });
component('HeroValue', { mapping: 'App.xaml HeroValue：Barlow Condensed Black Italic、92 DIP、LineHeight=76、Tabular；SideValue=28、RowValue=34、RowValueSmall=25、HeroUnit=26。Mini 主功耗28、百分比MiniValue22。', variants: '主功耗92／累計28／監測列34／雙磁盤25／Mini主功耗28與百分比22' });
component('按钮与复选框', { usage: 'PrimaryButton：SolidChipTemplate、AccentBrush／OnAccentBrush、11 DIP、SemiBold、Padding=14,5、CornerRadius=6；悬停整体Opacity=0.86、按下位移2/2 DIP。SecondaryButton：CardBrush／HairBrush、11 DIP、Padding=12,5、CornerRadius=6，悬停HoverBrush。QuietButton用于Reset totals。IconButton透明、Padding=5,3。键盘AccentFocusVisual：描边1.5 DIP、圆角8。16×16复选框CornerRadius=4，强调填色，保留自身形状，不套药丸规则。' });
component('累计时段选择', { mapping: 'App.xaml PeriodChip：CornerRadius=4、Padding=7,2、选中AccentBrush／OnAccentBrush；四个时段共管Energy／Carbon／Cost。', usage: '今日／7天／30天／全部使用同一个选择；7／30天为滚动区间，Mini跟随当前选择。保留4 DIP芯片圆角；监测缺口与旧每日分布不补算。' });
component('底色與抬起面（兩套皮膚）', { mapping: 'Theme.Apply 发布两套语义画笔；App.xaml 的静态资源仅用于启动，Theme.Apply 优先。PAPER POP 的HoverBrush由WPF #14000000换成CSS #00000014；VOLT使用#333330。Popup／Tooltip共用CardBrush，浮动设置／Mini／Splash则用PopPaper变体。' });
component('功耗數字的來源標記', { mapping: 'MainViewModel.TotalWattsValue／CpuPowerText／GPU PowerText 纯数值，不带 ~，正常估算不加曲线脚注。PowerEstimateHelp仅在Settings DataInfoTab。MainWindow使用StatBody；MiniWindow有独立紧凑布局，共用同一VM读数。底层SensorDetails和功率范围保留，不在Process展示。' });
component('Info 硬體資訊卡片網格', { mapping: 'InfoView使用PageContent（12/4/12/12 DIP）。Refresh靠右、下间距8 DIP，无Info大标题、装饰线和info_hint。RowUniformPanel Gap=16，InfoCardColumnsConverter最少2列最多4列；卡片Height=280、Card.Padding=16、CornerRadius=2。只有单张卡时实际铺一列。详情面板以DetailsContainer可用宽度扣除边距后限制。' });
component('主分頁內容與捲動邊界', { usage: '五个主分页共用PageContent。正常首页仅一层底部12 DIP；NET最后一列局部BorderThickness=0，其余LoadRow分隔保留。内容真正超出才出现Auto滚动条，短窗口仍可到达末行。' });
for (const c of data.components) if (!c.mapping) c.mapping = c.source ?? '詳見產品源碼對應元件；不以編輯器 App 樣式推導。';
guideline('儀表板末列收尾', { mapping: 'App.xaml StatBody 的最后 NET Border 使用LoadRow、局部BorderThickness=0；其他LoadRow保留分隔线。' });
guideline('主题与源码来源', { description: 'Theme.Apply 是运行时颜色来源；默认PAPER POP（paper）、VOLT（volt）是覆盖模式。Token保存实际语义画笔与现存控件效果；同值角色可共用画笔，不能仅因为没有独立颜色就写成尚未实现。' });
guideline('模板结构与多主题／多模式', { description: '保留当前schemaVersion 5结构与未知字段。遍历当前所有themes，记录PAPER POP／VOLT的真实值；只有产品确无语义实施位置的参数保留为未来设计。完整主窗口与独立Mini是运行时界面；Compact相关未引用样式只作旧实现记录。', source: 'design-system/AI.md; StatusMonitor/Views/Theme.cs; StatusMonitor/MainWindow.xaml; StatusMonitor/MiniWindow.xaml' });
guideline('字级来源与未使用资源', { description: 'App.xaml／MainWindow.xaml 的实际Style和TextBlock属性为字级权威，未被引用的system:Double不是实施入口。现行：HeroValue92／RowValue34／SummaryValue30／SideValue28／HeroUnit26／RowValueSmall25／MiniValue22／SettingsHeader20／分区与确认标题17／CardTitle13／Label与RowTag12／RowSub11／MicroLabel与RowName10。Info已无28 DIP大标题。' });
guideline('形状：药丸与卡片两种', { description: '此条现记录Momo的实际形状：按钮与输入框6 DIP、Info卡片2 DIP、浮动设置12 DIP，产品不使用药丸按钮。', source: 'StatusMonitor/MainWindow.xaml; StatusMonitor/App.xaml; StatusMonitor/MiniWindow.xaml; StatusMonitor/SplashWindow.xaml', usage: 'Card=2；导航芯片／Info标签／MiniIconButton=5；按钮／TextBox／ComboBox=6；权限提示／Mini=8；Settings／Close／Driver／Splash=12。复选框与时段芯片保留4 DIP例外。布局由Padding及内容撑开，不套未来36 DIP高度。', verification: verified });
guideline('语义色只有 ink / accent / alarm', { description: '运行时共享画笔为Ink／Muted／Accent／OnAccent／Alarm及背景边线；ToneBrushConverter在Ink与Alarm之间选择。权限提示另有已实现的固定#FEF3C7背景与#101513文字，作为warning语义记录。不存在独立success/info色板；未来颜色保留并明确未实现，不用编辑器自己的样式代替。' });
guideline('应用检查范围', { description: '每次GUI变更同任务同步design-system.json中的相关值、source、mapping并更新派生文档；设计系统由产品反向同步时不修改产品源码。不把编辑器App的外观记录为Momo设计。检查PAPER POP／VOLT、主窗口／Mini、设置与展开状态。', source: 'AGENTS.md; design-system/AI.md' });
data.project.designStatus = 'synced-from-product';
data.project.lastSyncedAt = now;
data.project.importScope = '2026-10-07 按最新WPF产品反向同步：实际Theme.Apply、XAML Style、五分页、Settings、Fans、Mini及页面滚动。当前实现覆盖旧参考值和旧mapping；未实现的设计参数仍保留。没有修改产品源码，没有读取或应用恢复默认预设。历史基线保留于design/guidelines/import-baseline.json。';
data.updatedAt = now; data.updatedBy = 'Codex';
const changes = Object.entries(data.tokens).filter(([key, value]) => value !== before.tokens[key]).map(([key, value]) => ({ key, before: before.tokens[key], current: value, baseline: baseline.tokens?.[key] ?? null, source: data.definitions[key].mapping }));
const report = { direction: 'product-to-design', createdAt: now, actualTokens: [...actualTokens], futureTokens: Object.keys(data.tokens).filter(k => !actualTokens.has(k)), changes, themeBefore: before.themes, themeCurrent: data.themes, productSourceChanged: false, verification: verified };
data.reconciliation = { direction: 'product-to-design', designRevision: designHash(data), recordedAt: now, reportedBy: 'Codex', verification: verified, evidence: 'dev/verification/design-system-current/reconciliation.json' };
validate(data);
if (await readFile(file, 'utf8') !== original) throw new Error('Design changed during merge');
const sourceBefore = JSON.parse(await readFile(join(evidenceDirectory, 'source-before.json'), 'utf8'));
for (const entry of sourceBefore) if (createHash('sha256').update(await readFile(join(root, entry.path))).digest('hex').toUpperCase() !== entry.sha256) throw new Error('Product source changed during extraction: ' + entry.path);
const temporary = join(directory, '.design-system-' + randomUUID() + '.tmp');
try { await writeFile(temporary, JSON.stringify(data, null, 2) + '\n', { flag: 'wx' }); await rename(temporary, file); }
finally { await unlink(temporary).catch(() => {}); }
const checked = validate(JSON.parse(await readFile(file, 'utf8')));
if (checked.reconciliation.designRevision !== designHash(checked)) throw new Error('Design revision mismatch');
await writeDesign(directory, checked, designHash(checked), join(directory, 'design'));
await writeFile(join(evidenceDirectory, 'reconciliation.json'), JSON.stringify(report, null, 2) + '\n');
console.log(JSON.stringify({ changedTokens: changes.length, actualTokens: actualTokens.size, futureTokens: report.futureTokens, designRevision: designHash(checked), productSourceChanged: false }));
