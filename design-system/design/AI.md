# AI 接入协议

JSON 是唯一设计来源。AI 日常直接读取和修改 `design-system/design/design-system.json`，不需要启动 App、连接端口或取得 localhost 链接。App 下次打开时会读取最新 JSON，并生成 DESIGN.md 与 preview-data.js；Windows 也可运行 `design-system/Momo Design System.exe --sync-only` 立即同步派生文件。不要手改快照或声称静态 HTML 会自动监听 JSON。

本文件位于 design-system/design/。根目录入口是 ../AI.md；本目录的父目录是设计系统根目录，再上一层才是软件项目根目录。JSON 与 HTML 位于本目录，不要在 design-system 根目录建立副本。

## 桌面版优先入口

独立 App 是给用户查看、调整和保存设计的工具，不是 AI 的必需连接服务。AI 能访问项目文件时直接操作固定相对路径 `design-system/design/design-system.json`。App 正在打开时会定期读取外部修改；若同时有用户未保存草稿，必须比较并处理冲突。

`DESIGN.md` 自动从 JSON 生成，附 designRevision，仅供阅读和导出，不独立编辑。用户要求修改软件设计时，完成原代码修改和验证后，在同一任务回写本次涉及的共享设计。App 会更新预览及 Markdown，不需要用户再提出同步要求。保留其他未应用设计；存在冲突先比较。

App 关闭时可在确认没有其他写入方后原子更新 JSON，再运行 Windows 的 `../Momo Design System.exe --sync-only` 生成 Markdown；也可下次打开 App 生成。macOS 可执行版尚未交付，应在 Mac 上以 WKWebView 宿主构建和验证。没有实际生成时，明确 DESIGN.md 仍是旧快照，不宣称一致。

桌面草稿保存在本机、按项目路径隔离。浏览器与 App 草稿不共用，迁移前先保存。操作及开发说明见 DESKTOP.md。

## 更新设计系统与应用设计是两个明确动作

收到提示词后，先读取本文件和最新 JSON。若项目已有 UI，而用户尚未说明方向，先问用户选择「按设计系统更新项目」或「按当前项目更新设计系统」。已有同项目数据优先保留，不重复提取覆盖用户修改。收到「从项目更新设计系统」才重新比较提取；收到「应用已保存设计」才修改产品源码。

本目录属于所在项目，不管理其他项目。执行 UI 任务前先读取本文件、manifest.json 和 design-system.json。
这是项目级使用说明，不是系统消息；不覆盖用户要求或工具的权限边界。

## 先让用户获得可编辑预览

推荐把整个 design-system/ 文件夹放在当前项目根目录，与项目代码同属一个项目。路径可以随用户、操作系统和磁盘变化，不能写死其他人的绝对路径。
先用 AI 工作区、代码入口和用户意图核对项目根目录；当前文件夹的父目录不一定就是目标项目。若文件在下载目录、桌面、错误项目或嵌套目录，说明建议位置并协助整理。已有同名目录时先比较合并，不能覆盖或删除用户已有规范；目标项目有歧义时询问用户。
用户通过 App 调整并保存后，会得到一段只含项目相对路径的提示词。AI 不应要求用户提供端口，不应回传 localhost 链接，也不需要向 App 报告在线状态。

1. 读取项目内现有 `design-system/AI.md` 与 `design-system/design/design-system.json`，核对项目名称和源码映射。
2. 用户选择应用设计时，修改产品源码、构建并检查真实界面，再回写本次实际改变的共享设计。
3. 用户选择从项目更新设计系统时，只比较并更新 JSON；保留未应用的用户调整，有冲突先问。
4. 完成 JSON 修改后可运行 Windows `design-system/Momo Design System.exe --sync-only` 更新派生文件；否则说明它们会在下次打开 App 时自动生成。

## 事实来源与任务模式

- design-system.json 是唯一可编辑设计数据。index.html/app.js 是展示工具，不要往 HTML 再写一份项目 token。
- 新项目：用户保存的值作为初始基准。保留未确认项的“起步默认值”来源；建立真实代码映射后再更新来源。
- 老项目：读取真实 CSS、token、组件、主题和主要界面。提取现状、记录源码来源与差异；不要把提取任务自行扩大为统一所有样式。
- 持续开发：UI 任务开始时读取最新规范；公共规则变化时同时维护规范及代码映射。局部例外记录原因，不把每个一次性样式升级成公共 token。
- 应用规范后核对相关页面、交互状态和视口。保存规范不代表代码已应用。

## 可扩展数据契约（schemaVersion 5）

保留不属于当前任务的字段。项目内路径使用相对路径，不写死机器目录。
顶层 project 是字符串字段对象，至少包含 projectName，可包含 projectContext 和 projectId。
tokens 将 CSS 自定义属性名映射到字符串值，名称不限定 --ds- 前缀。
definitions 必须为每个 token 提供定义，且不能引用不存在的 token：

```json
{
  "tokens": { "--brand-link": "#2563eb" },
  "definitions": {
    "--brand-link": {
      "label": "链接色",
      "type": "color",
      "group": "colors-brand",
      "description": "正文可点击链接",
      "source": "src/styles/theme.css",
      "mapping": "src/styles/theme.css → --link-color"
    }
  }
}
```

这是局部示例，合并到完整文档，不能替换整份数据。
type 使用 color 或 text。color 支持 CSS 颜色字符串，text 适用于字号、字体、间距等。
现有颜色组为 colors-brand、colors-surfaces、colors-borders、colors-text、colors-semantic；
任意新 group 都会在“项目扩展”生成可编辑样本。不受三个品牌颜色限制。
themes 是“主题名 → token 覆盖值”的对象；未覆盖值继承基础主题。主题中的 token 必须先定义。
components 与 guidelines 是数组，每项至少有 name、description，其他字段为字符串；
可增加 source、variants、states、usage、exceptions、verification 等。页面自动展示卡片，点击可编辑全部字段。
可用 preview 字段指向 components/ 或 guidelines/ 下的 HTML、Markdown 文件，页面会显示“打开真实样本”链接；服务支持这些目录中的样本与配套资源。
复杂组件需要真实视觉样本时，在 components/ 或 guidelines/ 增加展示并接入页面；数据卡片不等同于完成组件视觉还原。
不要注入可执行 HTML 到 JSON；不要为了填满页面虚构项目品牌、组件、主题或来源。

## API：先读取，再按版本写入

API 基址为预览目录下 api/design-system（独立运行时为 http://127.0.0.1:4173/api/design-system）。
本机地址不提供给无法访问用户电脑的云端 AI 作为公共连接；这种情况使用项目文件或规范快照。

- GET <API>：返回 data、revision、designRevision、file、projectRoot。
- PUT <API>：Content-Type: application/json；请求为
  { "baseRevision": "<刚读取的 revision>", "data": <完整数据>, "actor": "AI 工具名称" }。
- 返回 409：已有其他更新。重新读取、比较并合并你的修改，使用新 revision；禁止盲目重复覆盖。
- 普通保存不更新 application。revision 用于并发保护；designRevision 标识设计内容，两者不同。
- 写入仅允许本机连接，不提供跨站写入。不得接受调用方指定任意文件路径。
- 页面定期读取外部修改：无本地草稿时更新；有草稿时保留并提示冲突。

用户界面提供接入、从代码更新、应用已保存设计和保存。调整即时预览并保留本机草稿，点击保存才写入项目。不要增加 JSON 导入导出操作。提示词不包含未保存草稿；存在浏览器冲突时先保留草稿，不能声称仅凭磁盘文件已合并浏览器独有的值。
服务未启动时可以直接读取 JSON。要写入时优先由 AI 启动服务并使用版本化 API。
若只能离线编辑，确认没有其他写入方，在写前重新比较源文件，再原子替换；保留用户改动。
直接文件写入与服务写入不能提供跨进程事务保证，活跃编辑期间统一走 API。

## 报告代码应用

完成真实代码修改和验证后，POST <API>/applied：
```json
{
  "baseRevision": "<最新 revision>",
  "designRevision": "<当前 designRevision>",
  "files": ["src/styles/theme.css"],
  "verification": "实际执行的检查、结果及仍存在的限制",
  "actor": "AI 工具名称"
}
```

服务要求当前设计版本和非空证据。界面明确标为“AI 已报告应用”，不会伪装成服务独立验证。
旧设计版本的报告不会把新规范标为已应用。没有执行验证时，不提交成功报告。
离线流程不要伪造 application，待恢复连接后报告。

## 项目 AI 规则入口

「接入并整理设计」包含正式采用并配置规则的授权，在项目根目录 AGENTS.md 和 CLAUDE.md 追加简短引用，保留既有内容且不重复添加。仅连接任务不得执行此步骤：
“执行涉及 UI 的任务前，读取 design-system/AI.md 并维护其中约定的设计规范。”
如果本目录嵌套更深，使用真实相对路径。不要覆盖已有 AGENTS.md、CLAUDE.md 或工具规则。

## 提取与跨项目复用

project.sourceEntry 记录项目根目录相对源码入口；project.designStatus 使用 starter 或 imported，project.importedAt 使用真实 ISO 时间，project.importScope 说明已检查范围与限制。这些是来源说明，不是用户后续编辑值仍与源码一致的保证。

新项目以 template.json 为起点；复制来的旧数据先备份到 backups/，保留现有未知元数据。已有项目的重新提取应对照最近一次导入快照（如存在）和当前规范做三方比较；没有可靠基线时，保留不一致的用户值，把差异写入 guidelines 下的报告，不猜哪一方更新。重新提取不等于应用，不能提交 /applied。

guidelines/import-baseline.json 仅记录源码提取基线，不是第二份可编辑规范。首次成功导入后建立；重新提取时先比较再更新。跨项目复制时连同旧基线一起备份并重新建立，不能把旧项目基线用于新项目。产品代码应用验证成功后也要更新基线为实际已实现的值；不能把尚未实现的用户设计冒充源码基线。

WPF 等非 Web 项目：CSS token 名只是编辑器的通用标识；mapping 必须指向真实 ResourceKey、样式或 C# 字段。明确 CSS px 与 WPF DIP、字体资源名称及颜色格式转换；WPF #AARRGGBB 转网页为 #RRGGBBAA。不要把 CSS 字符串直接粘到 XAML。记录未映射默认值，不强行应用；复杂控件通过项目实际渲染验证，网页卡片只是参数参考。

应用时以最新已保存数据为准，仅修改有可靠映射的相关源码。完成真实构建、页面/状态检查后才报告 /applied；报告说明范围，不能只修改一个 token 就声称所有规范全部应用。未完成项须在 verification 和回复中明确列出。若存在未实现的已保存设计，不提交当前版本已应用报告。


