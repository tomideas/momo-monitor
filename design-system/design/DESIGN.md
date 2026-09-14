# Momo Monitor — Design System

> 自动生成；请通过编辑器或修改 design-system.json 更新，不要独立编辑本文件。
> Design revision: 394b841076a5280ebb81af2b180d50df1a1c2b2e20d7e3f13e2a21a86f658430

## 项目与提取范围

- **projectName**: Momo Monitor
- **projectContext**: WPF 桌面监控 · PAPER POP / VOLT
- **projectId**: momo-monitor
- **sourceEntry**: StatusMonitor/StatusMonitor.csproj
- **designStatus**: imported
- **importedAt**: 2026-09-13T07:45:47.035Z
- **importScope**: 源码提取：Theme.cs、App.xaml、MainWindow.xaml、AppFont.cs、Motion.cs；未核对 EXE 与源码构建一致性，未完成原生视觉验收。

## 设计参数

| 参数 | 值 | 用途 | 来源与代码映射 |
|---|---|---|---|
| --ds-primary | #14b8a6 | 强调色 — 基础主题 PAPER POP；VOLT 使用深色覆盖。 | StatusMonitor/Views/Theme.cs; StatusMonitor/Views/Theme.cs Apply → AccentBrush / AmberBrush；同时核对 App.xaml 的启动资源。 |
| --ds-primary-hover | #0d9488 | 悬停色（Hover） — 强调控件在悬停与高强调互动时使用。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-primary-soft | #e1f5f3 | 选中背景（Selection） — 选中背景使用强调色的浅色版本，避免与主要动作争夺注意力。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-processing | #d97706 | 处理中（Processing） — 用于生成、等待和异步任务，不与警告含义混用。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-processing-border | #f59e0b | 处理边框（Processing Border） — 用于处理中状态的边框、进度与较轻强调。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-processing-soft | #fff7ed | 处理浅色（Processing Soft） — 用于处理中提示的浅色背景。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-workspace | #f3f3f3 | 工作区（Workspace） — 用于编辑区、侧栏分区或第二层背景。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-background | #faf9f6 | 窗口背景 — 基础主题 PAPER POP；VOLT 使用深色覆盖。 | StatusMonitor/Views/Theme.cs; StatusMonitor/Views/Theme.cs Apply → PaperBrush；同时核对 App.xaml 的启动资源。 |
| --ds-surface | #FAF8F3 | 卡片表面 — 基础主题 PAPER POP；VOLT 使用深色覆盖。 | StatusMonitor/Views/Theme.cs; StatusMonitor/Views/Theme.cs Apply → CardBrush；同时核对 App.xaml 的启动资源。 |
| --ds-heading | #171615 | 主要文字 — 基础主题 PAPER POP；VOLT 使用深色覆盖。 | StatusMonitor/Views/Theme.cs; StatusMonitor/Views/Theme.cs Apply → InkBrush；同时核对 App.xaml 的启动资源。 |
| --ds-text | #5a5751 | 次要文字（Secondary） — 正文说明和次要信息使用的暖灰文字。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-muted | #8e8e8e | 次要文字 — 基础主题 PAPER POP；VOLT 使用深色覆盖。 | StatusMonitor/Views/Theme.cs; StatusMonitor/Views/Theme.cs Apply → MutedBrush；同时核对 App.xaml 的启动资源。 |
| --ds-tertiary | #b0aba5 | 禁用文字（Disabled） — 不可操作或最低强调信息使用的文字颜色。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-border-soft | #f2f1ef | 浅线条（Soft Border） — 用于正文分隔或低干扰区域边界，比普通线条更轻。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-border | #ebebeb | 细边线与轨道 — 基础主题 PAPER POP；VOLT 使用深色覆盖。 | StatusMonitor/Views/Theme.cs; StatusMonitor/Views/Theme.cs Apply → HairBrush / TrackBrush；同时核对 App.xaml 的启动资源。 |
| --ds-border-strong | #d7d4cf | 强调线条（Strong Border） — 用于需要更清楚分隔的边界、选区轮廓或较强结构线。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-success | #087a5b | 成功（Success） — 用于完成、通过和可用状态。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-warning | #d97706 | 警告（Warning） — 用于注意、等待和可能影响结果的状态。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-danger | #dc2626 | 超标警示 — 基础主题 PAPER POP；VOLT 使用深色覆盖。 | StatusMonitor/Views/Theme.cs; StatusMonitor/Views/Theme.cs Apply → AlarmBrush；同时核对 App.xaml 的启动资源。 |
| --ds-font-ui | Inter, ui-sans-serif, -apple-system, BlinkMacSystemFont, "PingFang SC", "Microsoft YaHei", sans-serif | 界面字体 — 网页为参数参考；WPF 长度单位为 DIP，应用时去掉 px 并核对实际样式。 | StatusMonitor/Settings/AppFont.cs; StatusMonitor/Settings/AppFont.cs → Resolve；App.xaml AppFontFamily；保留打包字体路径 |
| --ds-font-mono | "JetBrains Mono", ui-monospace, SFMono-Regular, Menlo, Consolas, monospace | 等宽字体 — 网页为参数参考；WPF 长度单位为 DIP，应用时去掉 px 并核对实际样式。 | StatusMonitor/App.xaml; StatusMonitor/App.xaml → MonoFont；保留打包字体路径 |
| --ds-display-size | 28px | 展示字号（Display Size） — 展示标题只用于页面主标题或品牌级信息。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-title-size | 20px | 标题字号（Heading Size） — 标题通过字号与字重建立层级，不只依赖颜色。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-font-size | 13px | 窗口默认字号 — 网页为参数参考；WPF 长度单位为 DIP，应用时去掉 px 并核对实际样式。 | StatusMonitor/MainWindow.xaml; StatusMonitor/MainWindow.xaml → Window.FontSize |
| --ds-label-size | 12px | 标签字号（Label Size） — 标签保持紧凑清晰；微型文字只用于元数据和短提示。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-micro-size | 10px | 辅助字号（Micro Size） — 标签保持紧凑清晰；微型文字只用于元数据和短提示。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-display-weight | 800 | 展示字重（Weight） — 展示标题只用于页面主标题或品牌级信息。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-title-weight | 800 | 标题字重（Weight） — 标题通过字号与字重建立层级，不只依赖颜色。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-font-weight | 600 | 标签字重（Weight） — 标签保持紧凑清晰；微型文字只用于元数据和短提示。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-line-height | 1.55 | 正文行高（Line Height） — 正文需要兼顾中文密度、长标签与不同屏幕宽度。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-space-unit | 2px | 基础间距（Space Unit） — 参考项目采用紧凑的 2px 基础单位，再用倍数建立层级。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-control-height | 36px | 控件高度（Control Height） — 按钮、输入框和下拉框共享基础高度与控件圆角。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-radius-control | 7px | 控件圆角 — 网页为参数参考；WPF 长度单位为 DIP，应用时去掉 px 并核对实际样式。 | StatusMonitor/App.xaml; StatusMonitor/App.xaml → TextBox / ComboBox ControlTemplate.CornerRadius；MainWindow.xaml ChipTemplate |
| --ds-radius-card | 10px | 卡片圆角 — 网页为参数参考；WPF 长度单位为 DIP，应用时去掉 px 并核对实际样式。 | StatusMonitor/MainWindow.xaml; StatusMonitor/MainWindow.xaml → Card.CornerRadius |
| --ds-radius-modal | 14px | 设置面板圆角 — 网页为参数参考；WPF 长度单位为 DIP，应用时去掉 px 并核对实际样式。 | StatusMonitor/MainWindow.xaml; StatusMonitor/MainWindow.xaml → 设置 Border.CornerRadius |
| --ds-radius-pill | 999px | --ds-radius-pill —  | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-content-width | 830px | 内容最大宽度（Max Width） — 先校准内容宽度与栏宽，再检查页面边距、表单基线和响应式变化。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-column-width | 260px | 辅助栏宽度（Side Column） — 先校准内容宽度与栏宽，再检查页面边距、表单基线和响应式变化。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-shadow-dropdown | 0 8px 24px rgba(46,45,42,.10) | 下拉阴影（Dropdown） — 静态卡片保持平面；下拉和弹窗才使用浮起阴影。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --ds-shadow-modal | 0 24px 64px rgba(46,45,42,.18) | 弹窗阴影（Modal） — 静态卡片保持平面；下拉和弹窗才使用浮起阴影。 | 起步默认值; 起步参考，未映射；不能直接套用 |
| --momo-on-accent | #FFFFFF | 强调色上的文字 — 基础主题 PAPER POP；VOLT 使用深色覆盖。 | StatusMonitor/Views/Theme.cs; StatusMonitor/Views/Theme.cs Apply → OnAccentBrush；同时核对 App.xaml 的启动资源。 |
| --momo-hover | #00000014 | 控件悬停背景 — 基础主题 PAPER POP；VOLT 使用深色覆盖。 | StatusMonitor/Views/Theme.cs; StatusMonitor/Views/Theme.cs Apply → HoverBrush；同时核对 App.xaml 的启动资源。 |
| --momo-dot | #E2DED3 | 点阵颜色 — 基础主题 PAPER POP；VOLT 使用深色覆盖。 | StatusMonitor/Views/Theme.cs; StatusMonitor/Views/Theme.cs Apply → DotPaper dot / App.xaml PopPaper；同时核对 App.xaml 的启动资源。 |
| --momo-font-display | "Barlow Condensed", "Noto Sans TC", sans-serif | 读数字体 — 网页为参数参考；WPF 长度单位为 DIP，应用时去掉 px 并核对实际样式。 | StatusMonitor/App.xaml; StatusMonitor/App.xaml → DisplayFont |
| --momo-hero-size | 92px | 主读数字号 — 网页为参数参考；WPF 长度单位为 DIP，应用时去掉 px 并核对实际样式。 | StatusMonitor/MainWindow.xaml; StatusMonitor/MainWindow.xaml → HeroValue.FontSize；App.xaml SizeHero |
| --momo-hero-weight | 900 | 主读数字重 — 网页为参数参考；WPF 长度单位为 DIP，应用时去掉 px 并核对实际样式。 | StatusMonitor/MainWindow.xaml; StatusMonitor/MainWindow.xaml → HeroValue.FontWeight = Black |
| --momo-hero-line-height | 76px | 主读数行高 — 网页为参数参考；WPF 长度单位为 DIP，应用时去掉 px 并核对实际样式。 | StatusMonitor/MainWindow.xaml; StatusMonitor/MainWindow.xaml → HeroValue.LineHeight |
| --momo-card-padding | 16px | 完整模式卡片内距 — 网页为参数参考；WPF 长度单位为 DIP，应用时去掉 px 并核对实际样式。 | StatusMonitor/MainWindow.xaml; StatusMonitor/MainWindow.xaml → Card.Padding；Compact 例外为 6 DIP |
| --momo-transition | 300ms | 数值过渡时间 — 网页为参数参考；WPF 长度单位为 DIP，应用时去掉 px 并核对实际样式。 | StatusMonitor/Views/Motion.cs; StatusMonitor/Views/Motion.cs → Transition / TimeSpan.FromMilliseconds；尊重 Reduced 和系统设置 |

## 主题：VOLT

未列出的参数继承基础主题。

- --ds-primary: #D4FF00
- --ds-background: #1F1F1C
- --ds-surface: #2A2A27
- --ds-border: #3A3A35
- --ds-heading: #E6E4DE
- --ds-muted: #BCB9AE
- --ds-danger: #FF3B14
- --momo-on-accent: #1F1F1C
- --momo-hover: #333330
- --momo-dot: #292925

## 设计原则与使用边界


### 主题与源码来源
- **description**: 基础主题为 PAPER POP，VOLT 为覆盖主题。运行时 Theme.Apply 是色彩来源，App.xaml 保留一致的启动资源。
- **source**: StatusMonitor/Views/Theme.cs

### CSS 与 WPF 转换
- **description**: CSS px 仅供预览；WPF 使用 DIP。WPF #AARRGGBB 转为网页 #RRGGBBAA，例如 #14000000 → #00000014。字体应保持 pack 资源引用。
- **source**: StatusMonitor/App.xaml

### 未映射值
- **description**: 保留原模板中未建立 mapping 的参数作为起步参考，不能声称它们来自 Momo，也不能批量套用到产品。
- **source**: design-system/template.json

### 应用检查范围
- **description**: 应用后验证 PAPER POP/VOLT、完整/Compact/Mini、设置焦点及展开状态。只有构建与实际检查后才能提交代码应用报告。
- **source**: StatusMonitor/MainWindow.xaml; StatusMonitor/MiniWindow.xaml

### 阴影例外
- **description**: 当前 MainWindow HardShadow 的 Opacity 和 ShadowDepth 为 0；通用网页阴影样本没有 Momo 映射，不代表产品使用这些阴影。
- **source**: StatusMonitor/MainWindow.xaml

## 组件与状态


### MetricCard
- **description**: 完整模式指标卡片；紧凑模式缩小内距并移除最小高度。
- **source**: StatusMonitor/MainWindow.xaml
- **variants**: 完整 / Compact
- **states**: 普通 / 数据缺失 / 超标（需结合绑定和 ToneBrushConverter）
- **usage**: 共享 Card/MetricCard 样式；不把页面中的每个数值都变成独立 token。
- **verification**: 源码已读；原生运行画面尚未复核。

### 底色與抬起面（兩套皮膚）
- **description**: 兩套皮膚都不用極端值。VOLT 底色 #1F1F1C、抬起面 #2A2A27、主文字 #E6E4DE（13.0:1）、次級 #BCB9AE（8.4:1）；PAPER POP 底色 #EDEAE1、抬起面由純白 #FFFFFF 改為暖近白 #FAF8F3。兩套皮膚的抬起級距因此一致（1.13 與 1.15）。
- **source**: StatusMonitor/Views/Theme.cs; StatusMonitor/App.xaml; StatusMonitor/MainWindow.FeatureChecks.cs
- **variants**: VOLT（本條規範對象）/ PAPER POP（未改，深字淺底不產生光暈）
- **states**: 一般文字 / 次級文字；標題列同步（Theme.ApplyCaption 內的值必須跟著 ground / ink / line）
- **usage**: 深色底不要用接近純黑。#0B0B0B 配 #F4F3EF 實測 17.7:1，就是會產生光暈（halation）的組合：瞳孔為暗場放大，亮字隨即過度刺激視網膜而暈開，任何人看久都累，有散光者會痛，而散光在人群中約佔一半。Material Design 的深色表面訂在 #121212 同理，且其資料指出不用純黑在 OLED 上的耗電差距約 0.3%。提底色並收斂主文字後為 13.9:1，仍遠高於 AAA 的 7:1。採暖中性灰而非 Material 的中性灰，因為本皮膚其餘色值都偏暖。 小字的問題不是顏色而是覆蓋率：10 DIP 搭灰階反鋸齒時，該行只有 21 個像素達到標稱色、99 個是反鋸齒邊緣，名目對比 6.73:1 但實測有效對比（墨色像素平均亮度）僅 3.45:1。單改顏色救不了覆蓋率問題，必須同時加大字級與字重——改為 11 DIP Medium 後有效對比 5.08:1、實色像素增加 85%。判讀這一層時要看有效對比，不是名目對比。 斷言改為以 WCAG 相對亮度實算 InkBrush 對 PaperBrush，並對深色皮膚設 16:1 上限，避免日後有人把底色改回接近純黑。 底色提過兩次（#0B0B0B → #191917 → #1F1F1C）。每次都要連帶抬 raise / line / dot / hover 與次級文字：底色變亮而其上的面不動，層級會塌進底色，次級文字的對比也會被吃掉。目前 raise 對底色 1.15:1、line 對底色 1.44:1、次級文字 8.4:1、主文字 13.0:1。 淺色皮膚同理不用純白。輸入框原為 #FFFFFF 落在 #EDEAE1 的底色上：級距 1.20 比深色皮膚的 1.15 更大，而且是溫度斷裂——暖色調裡放一塊中性白板，這才是輸入框刺眼的原因，不只是亮度。改為 #FAF8F3 後保住暖調、級距 1.13，與深色皮膚對齊；欄位的邊線本來就負責界定，不需要靠亮度差。文字落在欄位上為 18.3:1。 斷言新增三條守住這件事：抬起面不得為純白、底色不得為純黑、抬起級距須落在 1.05–1.25，兩套皮膚同一標準。
- **verification**: 已構建；每次調整都實算各層對比並以 --render 逐像素核對，且 35 項 UI 斷言（含 7:1 下限與深色皮膚 16:1 上限）於兩套皮膚皆通過。

### 啟動畫面（Splash）
- **description**: 460 × 260 DIP 圓角面板（圓角 12，無實線外框，改用投影加細線；視窗 484 × 284 留陰影空間），置中顯示品牌鎖定塊、掃動進度條與狀態文字；在開啟硬體監測服務期間代替空白視窗。
- **source**: StatusMonitor/SplashWindow.xaml; StatusMonitor/SplashWindow.xaml.cs; StatusMonitor/App.xaml.cs
- **variants**: VOLT 夜跑 / PAPER POP；中文 / English
- **states**: 掃動（預設）/ 靜態色塊（減少動效或系統關閉動畫時）
- **usage**: 啟動畫面存在的理由是遮蓋真實等待：LibreHardwareMonitor 開啟驅動約需 2–3 秒，原本這段時間主視窗已經顯示卻整個凍住。因此首次取樣改以 MainViewModel.StartAsync 在背景執行緒完成（與計時器共用 _sampleLock），主視窗在暖機完成後才顯示——否則進度條自己也會卡住，等於用一張靜止圖騙人。品牌鎖定塊沿用設定頁尾的組合並放大：標記 72 DIP，右側「Momo」用 Barlow Condensed Black Italic 52 DIP 取強調色，下方接描述詞（app_descriptor）12 DIP Bold。不要在此重複 app_brand，會讓「Momo」在同一畫面出現兩次。進度條為不確定式：背後只有一次不透明的驅動呼叫，任何百分比都是編造的；240 × 10 DIP 軌道置中，64 DIP 色塊來回掃動 900 ms。關閉順序固定為「先顯示主視窗、再關閉啟動畫面」，並顯式指派 Application.MainWindow——預設 OnLastWindowClose 之下，中間出現零視窗會直接結束行程。 啟動畫面不加外框：原本 2 DIP 的 Ink 描邊在深色皮膚上是一圈白框、淺色上是黑框。改為柔和投影加 HairBrush 細線，視窗 484×284 為 460×260 的面板留出 12 DIP 陰影空間。 與迷你面板、設定對話框同屬「浮動面」一類，三者共用同一套處理：皮膚底色、無實線外框、柔和投影加 HairBrush 細線。
- **verification**: 已構建並以 --render --splash 出圖核對 VOLT / PAPER POP 與中英雙語；另以實機啟動計時確認啟動畫面覆蓋約前 3 秒且未寫出錯誤記錄。原生動畫流暢度尚未逐格複核。

### HeroValue
- **description**: Barlow Condensed Black Italic 主读数，92 DIP 字号、76 DIP 行高、等宽数字。
- **source**: StatusMonitor/MainWindow.xaml
- **variants**: HeroValue / SideValue / RowValue 各有独立字号
- **states**: 数值更新 / 减少动画
- **verification**: 参数从源码提取；浏览器字体回退不代表 WPF 内嵌字体。

### 窗口尺寸下限与英雄区收缩
- **description**: 主窗口可手动缩放到 460 × 330 DIP（此前 720 × 480）。英雄区改为两个星号列：功耗列 MinWidth 104 / MaxWidth 236，读数列 2.4* MinWidth 190，列内左边距 18 DIP。 視窗的位置與尺寸會被記住：關閉時寫入設定，下次開啟回到原處，包含最大化狀態。
- **source**: StatusMonitor/MainWindow.xaml; StatusMonitor/App.xaml
- **variants**: 完整（460–∞）/ 迷你（258 固定）
- **states**: 宽裕（英雄区满尺寸）/ 收缩（Viewbox DownOnly 等比缩小主读数）
- **usage**: 英雄区的功耗列必须是星号列，不能用 Auto。Auto 列永远不交还宽度，92 DIP 的主读数会把用电／碳／电费整块挤出右边缘——这正是窗口此前无法拖到 720 以下的原因。上限 236 DIP 让主读数在宽窗口保持满尺寸，同时把读数块的起点固定在同一个 x；下限 104 DIP 配合 DownOnly 的 Viewbox，只缩小不放大。热门进程表的名称列取 MinWidth 120 DIP 并把横向滚动条设为 Auto：五个读数列宽度固定，窗口变窄时全部由名称列吸收，没有下限它会被压成一条缝。460 是实测下限，不是估值：在 VOLT / PAPER POP 两套皮肤与中英双语下出图核对，时段选择器四个筹码、双硬盘并排、趋势卡控件均不换行不裁切；再窄要先重排这几处。低于此尺寸用迷你面板，不要继续压缩完整模式。 記住位置有兩個容易做錯的地方，都已處理。其一，存的必須是 RestoreBounds 而非 Left/Top/Width/Height：最大化的視窗回報整個螢幕，最小化的回報 -32000，存下去之後下一次啟動會被判定為無效而默默不還原——結果是「記住位置」看起來時靈時不靈。其二，存下的座標所描述的桌面下次可能已經不存在（外接螢幕拔掉、筆電離開擴充基座、顯示器重新排列），還原到那裡看起來就跟程式沒啟動一樣；因此還原前先確認標題列仍落在某個螢幕的工作區內，否則放棄還原、照首次啟動置中。 判準不是「視窗與螢幕有無交集」而是「標題列有沒有一段抓得住」：右緣外只剩二十像素標題列的視窗，在算術上可達，在實際上抓不到。因此要求可見的標題列至少 60 × 16 DIP。 存檔時機在 Closing 的最前面，早於「縮到系統匣」的分支：縮到系統匣會取消關閉，只在真正退出時存的話，用系統匣的人永遠不會被存到。
- **verification**: 已构建并以 --render 在 460 / 540 / 720 / 900 / 1040 宽度出图核对仪表板、信息页、设置对话框与页尾进程表，另核对 460 × 330 下设置对话框的标题与页脚仍在屏内；原生拖拽手感尚未复核。 位置記憶的規則抽成不依賴視窗型別的模組並以 111 條純邏輯斷言中的八條涵蓋：單螢幕還原、左側第二螢幕在時還原、該螢幕移除後不得還原、標題列在螢幕上方時不得還原、只剩碎片時不得還原、仍抓得住時要還原，以及位置尺寸與最大化狀態必須能存活重啟、舊設定檔沒有位置資訊時必須照舊置中。多螢幕與拔除螢幕的情形本機無法重現，這正是規則被抽出來測的原因。

### 设置输入与选择器
- **description**: 设置对话框：按分组排列的标签 + 控件行，统一标签列 128 DIP、控件列上限 260 DIP；动态主题输入框、下拉选单、标签页，6 DIP 控件圆角。
- **source**: StatusMonitor/MainWindow.xaml; StatusMonitor/App.xaml
- **states**: 键盘焦点 / 悬停 / 展开 / 选中 / 勾选；禁用等状态需原生复核
- **usage**: 每项设置用一个 HeaderedContentControl（SettingRow 模板）表达，不再逐行重写 Grid 列定义，避免标签列与控件宽度逐行漂移。分组用 SettingGroup 小标题划分「外观 / 用电与费率 / 窗口与进程」。对话框按内容高度居中，中间行为星号高度以便滚动，标题与页脚常驻。品牌标记、名称与版本组成页脚的品牌锁定块（标记 34 DIP，名称与版本竖排），不放标题行；小于此尺寸的标记只是装饰，认不出品牌。不可逆操作（重置累计）使用 DangerButton（警示色描边、透明底），与主要操作「关闭」区分。 强调色在对话框里的含义与仪表板不同：仪表板上颜色只说「读数超标」，对话框没有读数，强调色表示「当前选中项与主要动作」——选中的标签页（下划线）、勾选后的复选框（强调色填充 + OnAccent 勾）、主按钮「关闭」（强调色填充）。不可逆操作仍用警示色描边。 「視窗與程序」分組現含「更新間隔」下拉，並在其下加一行說明字（Label 樣式、11 DIP）——這是設定頁唯一會改變程式自身耗電的項目，說明不可省。 「視窗與程序」另含「迷你面板保持置頂」勾選框，預設關閉：會蓋住其他視窗的行為應由使用者主動開啟，而不是先發生再讓他找地方關掉。改動要同時套用到已開啟的面板（MiniWindow.ApplyTopmost），面板是長期存活的單一實例，建構時讀到的值會過期。 對話框用的是皮膚底色（PopPaper，含點陣），不是 CardBrush：CardBrush 在淺色皮膚是純白，而底色是 #EDEAE1，對話框因此讀作一塊比所處模式更亮更冷的板子。改用底色後，同一皮膚裡儀表板、迷你面板、啟動畫面與設定對話框四個面都一致；輸入框與次要按鈕維持 CardBrush，於是它們相對對話框是抬起的，而不是溶進去——這反而把層級做對了。 對話框同樣不加實線外框（原為 2 DIP Ink，等於淺色皮膚上的黑框、深色上的白框）：背後的遮罩加上投影與 HairBrush 細線已經足夠分離。三個浮動面（迷你、啟動、設定）現在共用同一套處理：無外框、投影、細線、皮膚底色。
- **variants**: SettingRow（标签+控件）/ SettingGroup（分组小标题）/ SettingCheck（勾选项）
- **verification**: 源码已读并以 --render 出图核对一般页与监控页；原生运行画面尚未复核。

### TrendChart 与 MetricBar
- **description**: 自绘趋势及指标控件；实际绘制以 C# 控件为准。
- **source**: StatusMonitor/Views/TrendChart.cs; StatusMonitor/Views/MetricBar.cs
- **verification**: 已定位源码，尚未完成控件实现及所有状态提取。

### 迷你速览面板
- **description**: 258 DIP 寬的浮動速覽面板（視窗 278，外圈 10 DIP 留給陰影）；無實線外框，改以柔和投影加一道 HairBrush 細線分界。右上角為 ↗ 還原與 ✕ 關閉兩個無底色圖示鍵。
- **source**: StatusMonitor/MiniWindow.xaml
- **variants**: VOLT / PAPER POP；置頂 / 不置頂（預設不置頂）
- **states**: 普通 / 超标（沿用 ToneBrushConverter）/ 置顶
- **usage**: 图标代替文字标签、数值代替进度条——进度条的价值来自共享基线后互比长度，只有两列单值时它只占空间。磁盘取最满的一颗。窗口用 SizeToContent 高度自适应，所有行为 Auto；不要用星号行，会在读数下方留出读作空白的余量。整个面板可拖动，因此不放拖动握把。 完整模式现在可以拖到 460 DIP 宽，与迷你面板的 258 DIP 仍相隔一段；两者各管一档，不要把完整模式继续压向迷你面板的尺寸。 邊界用投影不用外框：原本的 Ink 實線在淺色皮膚上讀作一個黑框、深色皮膚上讀作白框，等於把面板框起來而不是讓它浮起來。現在用 16/3 DIP、30% 不透明的投影，配一道 HairBrush 細線——那是相對面板本身的低對比色階，不是黑也不是白。陰影需要視窗內留白才畫得出來，所以視窗寬 278、Border 外加 10 DIP Margin，可見面板仍是 258。 右上兩個鍵都不給底色：這麼小的面板上一個填色方塊會變成畫面最響的東西，而還原與關閉都不是重點；沿用主視窗頁首圖示鍵的做法。✕ 走的是主視窗自己的 Close，語意與標題列的 X 完全一致——結束程式，或在使用者開啟後縮到系統匣；在迷你面板另立一種「關閉」是陷阱。 「保持置頂」已移到設定，面板上不再放勾選框：那是設定一次就不再碰的選項，卻長期佔掉這個沒有餘裕的面板一整行。
- **verification**: 已構建並以 --render --mini 出圖，另以實機螢幕擷取核對 VOLT 與 PAPER POP 的投影與邊界（--render 只畫得出面板自身範圍，畫不出陰影與透明，所以必須實機截圖）。32 項 UI 斷言含「預設不置頂」「設定可對已開啟的面板生效」「關閉鍵存在」。

### 按钮与复选框
- **description**: ChipTemplate（透明/描边按钮）与 SolidChipTemplate（填充按钮）两套模板；复选框为自绘，勾选时强调色填充。
- **source**: StatusMonitor/MainWindow.xaml; StatusMonitor/App.xaml
- **variants**: PrimaryButton（强调色填充）/ SecondaryButton（描边）/ QuietButton（弱化描边，用于不可逆但非紧急的操作）/ IconButton（透明）/ CheckBox / PeriodChip（单选时段）
- **states**: 普通 / 悬停 / 键盘焦点 / 按下 / 勾选
- **usage**: 填充按钮用 SolidChipTemplate：悬停降低不透明度，不替换背景——替换会把标识主要动作的强调色丢掉。透明与描边按钮用 ChipTemplate，悬停加 HoverBrush 底色。ChipTemplate 的悬停不覆写 Foreground，否则会把 DangerButton 的警示色文字压回墨色。为复选框写具名样式时必须 BasedOn 隐式样式，否则会整体替换掉自绘模板、退回系统白底外观。 不可逆但非紧急的操作（重置累计）用 QuietButton 并配两步确认，不用警示色——警示色留给真正的异常状态，按钮涂红只会变丑且降低红色的含义。
- **verification**: 源码已读并以 --render 出图逐一放大核对标签页、复选框与按钮；悬停与按下状态按模板逻辑推导，未逐一原生复核。

### 累计时段选择
- **description**: 英雄区右侧三项累计（用电 / 碳足迹 / 电费）共用一个时段选择：今日 / 7 天 / 30 天 / 全部。
- **source**: StatusMonitor/App.xaml; StatusMonitor/Services/EnergyHistory.cs
- **variants**: 今日 / 7 天 / 30 天 / 全部
- **states**: 选中（强调色填充）/ 悬停 / 普通 / 记录不足（显示「每日记录自 MM-DD 起」）
- **usage**: 一个选择器同时管三项：用电、碳足迹与电费是同一测量的三种表达，让它们各自选时段没有意义。周与月是滚动窗口（最近 7 / 30 天）而非自然月，避免每月 1 号数字塌成接近零。仅按日累计瓦时；碳与电费仍按当前费率推导，与运行总计的既有行为一致。迷你面板没有空间放选择器，跟随仪表板的设置。 按日记录是随该功能上线才开始的，旧的运行总计没有每日分布，不能凭空摊派。当所选窗口早于记录起点时，在时段行旁注明记录起始日期——否则「7 天」少于「全部」会被读成故障。用电低于 10 Wh 显示一位小数，避免刚开始记录时显示成「0 Wh」。累计每 5 分钟落盘一次，不再只在退出时写出。
- **verification**: 源码已读并以 --render 出图核对；跨日切换与长期保留（400 天）按实现推导，未做跨日实测。

### 取樣間隔與閒置降頻
- **description**: 「一般 › 視窗與程序」新增「更新間隔」下拉（1 秒 / 2 秒（建議）/ 5 秒，預設 2 秒），下方接一行說明文字；沒有視窗在畫面上時自動改用 5 秒。
- **source**: StatusMonitor/MainWindow.xaml; StatusMonitor/Settings/AppSettings.cs; StatusMonitor/ViewModels/MainViewModel.cs; StatusMonitor/MainWindow.Features.cs
- **variants**: 1 秒 / 2 秒（預設）/ 5 秒；畫面上 / 閒置
- **states**: 可見（採用設定值）/ 無視窗在畫面上（採用閒置值，回到畫面時立即補一次讀數）
- **usage**: 這個下拉是設定頁裡唯一直接決定本程式自身耗電的項目，所以配一行說明，不要當成一般偏好省略。預設 2 秒有依據：一輪感測器實測約 82 ms，其中 GPU 驅動查詢佔大部分，因此速率減半等於自身負載減半；HWiNFO 出廠同樣是 2000 ms，Task Manager 把 1 秒叫「正常」、4 秒叫「低」，1–2 秒就是業界常態。 準確度代價已量測，不是估計：以 1 秒序列為基準做抽樣比較，2 秒的累計用電偏差在 0.5% 以內、5 秒在 1% 以內，而且沒有系統性方向（偏差均值 ±0.1%），屬隨機誤差，跑得越久越會互相抵消。因此閒置時降到 5 秒是安全的——此時沒有任何讀數被人看見，唯一要保住的只有累計用電。 閒置狀態由主視窗與迷你面板的可見性一起推算（RefreshIdleState），不要在每個 Hide/Show 呼叫點各自設定：隱藏到系統匣、最小化、切換迷你面板是三條不同路徑，遲早會漏掉一條。
- **verification**: 已構建並以 --render --settings 出圖核對新設定列；實機量測 release 版可見時 6.4% 單核、最小化後 2.3%、還原後回到原值，確認降頻會生效也會解除。29 項 UI 斷言與 48 項 FeatureChecks 全過。

### 風扇頁（唯一會寫入硬體的頁）
- **description**: 「風扇」為頂層頁面，排在「儀表板 / 信息」右邊；機器沒有可控風扇時該頁籤隱藏。每顆風扇一行，由左到右：所屬硬體圖示、名稱、目前轉速（一行中唯一放大加粗的數字）、「自動 / 自訂」兩顆 chip。選「自訂」時設定面板就在該列底下展開，含「依溫度調整」（開始提速溫度 + 全速溫度）或「固定轉速」，右側為「確定」。面板編輯的是設定的副本，按下「確定」之前不會有任何值送到風扇；確定後面板收合，設定改由該列自己的小字陳述（如「軟體控制中 · 50–90 °C · 34–100%」）。溫度來源不詢問使用者，由服務依風扇所屬硬體自行解析。
- **source**: StatusMonitor/Models/FanCurve.cs; StatusMonitor/Services/FanControlService.cs; StatusMonitor/ViewModels/FanProfileVm.cs; StatusMonitor/Settings/AppSettings.cs; StatusMonitor/MainWindow.xaml; StatusMonitor/MainWindow.Features.cs; StatusMonitor/App.xaml
- **variants**: 自動（預設）/ 依溫度調整 / 固定轉速；圖示六種：GPU 顯示卡、CPU 風扇（風扇＋右下晶片徽章）、機殼風扇（方框＋三葉）、Extra Flow（風扇＋單一箭頭）、AIO 幫浦（本體＋兩條外張水管＋水滴）、一般風扇（環＋三葉）。除 GPU 與幫浦外一律是風扇造型，未知名稱的插座也落在一般風扇，不會出現一列是裸硬體圖示。
- **states**: 韌體控制 / 軟體控制中 / 溫度讀不到（自動交還韌體）；自訂面板展開／收合（跟隨該列模式）；面板已編輯未套用（IsDirty）；面板開啟中（IsEditing）與「已設為自訂但面板收合」是兩個不同狀態
- **usage**: 版面收斂成 Macs Fan Control 的一行一顆風扇，不是 FanControl（Rem0o）那種每顆風扇六個數值欄位加獨立曲線物件與混合函式。六顆風扇的主機板要能一屏看完，細節只為正在調整的那顆展開。 溫度來源完全不問使用者。演進過程：十一個感測器的下拉 → CPU / GPU 兩顆按鈕 → 不問。理由是這個問題有可推導的正確答案：GPU 風扇跟自己那張卡（Hot Spot，它領先核心溫度），CPU 風扇跟 Package（逐核心讀數會因單一忙碌執行緒暴衝），機殼風扇跟 CPU 與 GPU 當下較高的那一個。最後一條才是關鍵——機殼風扇若只綁 CPU，會在長時間 GPU 負載時整段待機，而這正是小體積機殼真正會出事的情況；MSI 自家的系統風扇控制（FROZR AI Cooling）也是同時看 CPU 與 GPU 而非要使用者選。 每行最左側放一個圖示標示該風扇所屬硬體（CPU / GPU / 機殼），因為六顆風扇否則就是六行近乎相同的文字。圖示先放在名稱右側，那只是裝飾；放在名稱之前，它才是你沿著這一欄往下掃、用來找到目標風扇的東西。機殼風扇的圖示先做成三片彎曲扇葉，在 20 DIP 下糊成一團——與列圖示當年在 16 單位格上遇到的是同一個密度問題；改為外環＋三根直輻條＋輪轂後才站得住。 資訊階層照 Macs Fan Control：一行裡剛好一個數字放大加粗，就是這顆風扇當下的轉速，其餘全部是繞著它排的脈絡（RPM 與溫度次一級，模式與範圍再次一級）。先前四項事實擠在同一行等寬灰字裡，結果整頁存在的理由——轉速——反而最難找。 設定面板就在該列底下，不用彈窗。彈窗版本做過一輪也上過機：在只有一顆風扇的機器上看起來合理，但在七顆風扇的主機板上，面板正好蓋住你要拿來比較的那幾列，而比較正是這一頁的用途。面板一度改成即時生效（沒有確認步驟），這是錯的：打到一半的溫度會立刻驅動風扇。現在面板編輯副本，右側「確定」才送出，等於把彈窗版本的安全性搬進內嵌版本，而唯一的例外是「自動」——交還韌體不需要確認，那是安全的方向。 溫度欄位用 WrapPanel 而非水平堆疊：460 DIP 英文介面下兩組「標籤＋欄位＋單位」放不進一行，水平堆疊會讓第二個欄位直接跑出視窗外。 「讀取風扇現況」與「驅動風扇」是兩個方法：原本合在一起，導致預覽渲染（被禁止驅動硬體）連轉速都只能顯示破折號，而切進風扇頁時也要等到下一個取樣週期才有數字。 這條規則試錯過五輪：可編輯曲線節點表（太繁瑣）→ 具名預設（選項反而變多）→ 感測器下拉（十一個項目答兩個答案）→ CPU/GPU 兩顆按鈕（仍在問可推導的事）→ 不問，面板內嵌。判準始終是選項數量，不是功能多寡。 圖示依感測器名稱分派，因為主機板對插座唯一提供的資訊就是標籤：Pump → 幫浦、CPU → CPU 晶片、Flow → 氣流、Chassis/Case/System → 機殼、其餘 → 一般風扇。判斷順序有意義：Pump 先於 CPU，否則「CPU Pump」會被畫成風扇。 畫這四個圖示時的判準是輪廓，不是內部細節——22 DIP 下活下來的是外框。三次失敗都來自同一個原因：CPU 風扇先畫成「風扇＋方形輪轂」，與圓形輪轂在該尺寸下無法區分；改成「風扇＋四根對角接腳」後，眼睛會把對角的接腳跨過圓環連成一條線，看起來像圓上打了個叉；兩支並排的氣流箭頭在 22 DIP 併成一塊；幫浦的兩條水管畫成垂直時併成單一根梗，整個圖示看起來像燈泡。氣流改單一箭頭，幫浦水管改成明顯外張的 V。CPU 風扇一度改為沿用既有的 CPU 晶片圖示，被否決——這一頁每一列都是風扇，圖示就該是風扇造型，不能有一列是裸硬體。最終作法是把 CPU 標記移出輪子：風扇維持風扇，晶片縮成右下角一枚徽章，是獨立形狀而非輪內細節，因此在 22 DIP 下仍站得住。 名稱比對同時涵蓋縮寫：主機板常以絲印名回報（CPU_FAN1、CHA_FAN2、SYS_FAN3、W_PUMP+、AIO_PUMP），LibreHardwareMonitor 對沒有友善名稱表的晶片會原樣透出，所以 CHA / SYS / AIO / Water 都要認。未知名稱一律落在一般風扇造型。 面板在兩種模式下必須等高。作法：溫度欄位列用 Hidden 而非 Collapsed（保留空間），固定轉速的數值欄位放在它自己那顆 radio 的同一行。兩者合起來讓面板高度固定，切換模式時下面的列不會在指標底下位移。「確定」放在獨立的右欄垂直置中，因此加上它完全沒有增加高度。 確定後面板必須收合。六顆風扇各自攤開一組控制項，整頁就是一面控制牆——這一頁的用途是比較，不是同時編輯六顆。因此「chip 亮起（IsCustom）」與「面板開著（IsEditing）」拆成兩個狀態：前者是風扇的狀態，後者是面板的狀態。收合後設定不能消失，所以該列的小字從「模式 · 範圍」改為「模式 · 設定 · 範圍」，不必展開就看得到自己設了什麼。 自訂 chip 改為單向綁定加 Click 處理：已亮起的 radio 被點擊時仍會發出 Click 但不改變狀態，正好是「對已設定過的風扇重新打開面板」需要的行為。
- **verification**: 已構建；純邏輯 101 條斷言，含以 ASUS ROG STRIX B850-I 實際回報的插座名稱（CPU Fan / Chassis Fan / Extra Flow Fan / AIO Pump）驗證圖示分派與跟隨對象——本機無任何主機板風扇，沒有這組測試這些規則等於未經檢查就出貨。90 項 UI 斷言於兩套皮膚通過，含「Nav 順序必須是儀表板→信息→風扇」「風扇頁籤剛好在有可控風扇時可見」「風扇至少要有一個可跟隨的溫度類別」「GPU 風扇必須解析到自己那張卡的溫度」「六個圖示 Geometry 必須都解析得到」；版面四條：硬體圖示必須整個落在名稱左邊、轉速字級 ≥18 且為 Bold、轉速字級至少是名稱的 1.5 倍、轉速必須用全強度 Ink；面板三條：自訂面板只能開在自己那一列、切回自動必須收合、460×330 下面板內每個欄位都必須完整落在畫面內。撰寫斷言時抓到一個自己的錯誤：切換到風扇頁會重建列的 view model，斷言若在切換前取得參考，操作的是已經離開畫面的物件。寫入路徑以 --fan-test 在實機 NVIDIA 卡驗證。機殼風扇圖示未在真實主機板上看過——本機主機板節點為空，無可控機殼風扇；使用者端的 ASUS ROG STRIX B850-I（五顆機殼風扇）畫面顯示圖示與版面成立，但軟體寫入未在該機驗證。 圖示另加三類斷言：六個圖示的兩層 Geometry 都必須解析得到、每個新圖示必須完整落在 24 單位格內（超出不會被裁切，只會畫到旁邊的名稱上）、六個列圖示的路徑必須彼此相異（六行畫得一樣就失去圖示欄的意義，而其中兩個一度確實近乎相同）。 另加一條原則性斷言：任何插座名稱——包含空字串與沒人預料過的標籤——都必須解析到已知的風扇造型之一。 另加四條：未按確定前面板不得碰到風扇、已編輯的面板必須回報自己尚未套用、確定必須把值交給風扇且之後不再有未套用內容、面板在兩種模式下高度差必須小於 0.5 DIP。 再加三條：確定必須收合面板但讓風扇維持自訂、面板收合時該列本身必須陳述設定值、已是自訂的風扇必須能再次打開面板。

### 關閉確認（隱藏還是退出）
- **description**: 按下視窗關閉鍵時出現的確認面板：標題、一句提問、「下次不再詢問」核取方塊，以及「完全退出 / 僅關閉視窗」兩顆按鈕。與設定對話框同一套外觀——同底色、無外框、遮罩加陰影。預設會問；勾了不再詢問就照當次選擇，之後不再出現。
- **source**: StatusMonitor/MainWindow.xaml; StatusMonitor/MainWindow.xaml.cs; StatusMonitor/MainWindow.Features.cs; StatusMonitor/Settings/AppSettings.cs
- **variants**: 兩個動作：僅關閉視窗（縮到系統匣繼續採集）/ 完全退出；核取方塊決定是否記住
- **states**: 詢問中 / 已記住為隱藏（AskOnClose=false, RunInTray=true）/ 已記住為退出（AskOnClose=false, RunInTray=false）
- **usage**: 從標題列看，隱藏與退出長得一模一樣，但兩個結果不能互換：一個讓累計用電繼續跑，另一個結束它。先前的行為是由設定裡一個核取方塊默默決定的——關閉鍵到底會做什麼，要打開設定才知道。 預設值選「詢問」而不是任何一種行為：無聲縮到系統匣，正是使用者回報「這程式關不掉」的那種行為；無聲退出則會在累計中途把數字結束掉。兩個預設都會弄丟東西，只有「問一次」不會。 「僅關閉視窗」是主要按鈕也是 Enter 的預設動作，因為它是安全的那一邊：數字繼續累計，而且雙擊系統匣圖示就回來了。系統匣圖示永遠存在（不受 RunInTray 影響），所以隱藏永遠救得回來。 記住選擇時必須同時寫入兩件事：不再詢問，以及當次選的是哪一個。只寫「不再詢問」會讓下一次關閉去照設定裡那顆系統匣核取方塊，而那不是剛剛選的東西。 設定裡新增「關閉視窗時詢問」核取方塊，與既有的「縮到系統匣」並列：前者決定問不問，後者是不問時的答案。在面板裡做的選擇會同步回這兩顆核取方塊。
- **verification**: 已構建。98 項 UI 斷言於兩套皮膚通過，其中關閉相關七條：全新安裝必須會問、面板出現時頁面必須停用、面板不得帶著已勾選的「不再詢問」出現、未勾選時作答不得改變下次行為、勾選後必須把「隱藏」或「退出」一起記住、設定裡的核取方塊必須跟著面板的選擇走、關閉面板必須把頁面交還。純邏輯 103 條斷言中兩條：舊設定檔載入後必須仍會詢問（不能默默變成不問），已記住的選擇必須能在重啟後留存。中英文與兩套皮膚均已渲染確認。

### 資料檔案位置（預設與可攜）
- **description**: 三個檔案：settings.json（偏好）、totals.json（累計用電 Wh）、energy-history.json（每日用電，約 400 天，「今日／7 天／30 天」由它算出）。預設存於 %APPDATA%\StatusMonitor\；在執行檔旁建立 momo-data 資料夾即切換為可攜模式，改存該資料夾。
- **source**: StatusMonitor/Settings/AppSettings.cs; StatusMonitor/Services/EnergyHistory.cs
- **variants**: 預設（%APPDATA%）/ 可攜（執行檔旁的 momo-data）
- **states**: 首次以可攜模式啟動時自動搬入既有資料；已有資料的可攜資料夾不覆蓋
- **usage**: 可攜是「選擇加入」而非預設。同一顆執行檔會被別人放進 Program Files，或放在雲端同步資料夾裡執行——預設就往執行檔旁邊寫檔，對這兩種情況都是壞事。建一個資料夾當開關，比加一個設定項好：它本身就是使用者能看見、能移動、能刪除的東西，不需要先打開程式才知道檔案在哪。 切換位置時必須搬既有資料。否則建立資料夾之後，累計用電與每日歷史看起來像被清空了——其實還在，只是程式不再看那裡。搬移只在目標資料夾為空時進行，因此不會蓋掉一份已經在用的可攜資料。 路徑只解析一次並快取：答案在行程存續期間不會改變，而每次存檔都重新判斷等於把一次目錄探測放進取樣路徑。 執行檔位置取自 Environment.ProcessPath 而非組件位置：單檔發佈下組件位置是空字串。
- **verification**: 已構建。純邏輯 115 條斷言中四條涵蓋解析規則：旁邊有 momo-data 時必須採用、沒有時必須維持 %APPDATA%、執行檔位置為 null 或空字串時必須退回 %APPDATA% 而非猜測。搬移路徑以實機驗證：把執行檔複製到臨時資料夾、建立空的 momo-data、啟動一次，三個檔案確實出現在該資料夾內。

## AI 工作方式

UI 工作先读 AI.md 与最新 design-system.json。修改软件后，同一任务回写实际变更的共用设计；遇用户未应用的设计差异先比较，不整份覆盖。DESIGN.md 是派生快照，版本不同时以 JSON 为准。
