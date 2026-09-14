# 从这里开始

本文件是当前项目设计系统的 AI 入口。用户双击本目录 `Momo Design System.exe` 打开编辑器；整份 design-system 随项目携带。

- `design/design-system.json`：唯一正式设计资料。UI 任务先读最新版本。
- `design/index.html`：查看与编辑画面，与 JSON 同目录；不要另建一份设计数据。
- `design/DESIGN.md`：从 JSON 自动生成的阅读版，不独立编辑。
- `design/preview-data.js`：从同一 JSON 自动生成的本地 HTML 预览快照，不独立编辑。App 保存、后台检查或离线同步时与 DESIGN.md 一起生成；HTML 与 App 共用同一渲染结构。
- `design/AI.md`、`design/CONNECT.md`：保存、接入、源码映射与冲突处理协议。
- `design/components/`、`design/guidelines/`：项目组件、规则与来源。
- `app/windows/`：Windows 运行所需的轻量文件；升级它不能覆盖 design/。
- `node_modules/native-source/`：原生宿主开发源码，不是设计资料，AI 日常无需读取。

AI 日常直接读取和修改固定相对路径 `design-system/design/design-system.json`，不需要 App 在线、连接端口或 localhost 链接。App 只供用户查看、调整和保存；打开时会读取 AI 写入的最新 JSON。

用户让 AI 修改软件后，同一任务仅回写相关共享设计，保留其他尚未应用的用户调整。App 自动更新预览和 DESIGN.md；App 关闭时按 design/AI.md 的离线协议操作。

修改 JSON 后可运行 `Momo Design System.exe --sync-only` 更新 DESIGN.md 与预览快照；否则它们会在下次打开 App 时自动生成。HTML 中的 CSS 默认值仅用于无数据时的占位，不作为日常独立编辑数据。

当前软件的源码路径相对本目录的父目录（项目根目录），不是相对 design/。首次采用时，将本入口引用加入项目原有 AGENTS.md 与 CLAUDE.md，保留已有规则。

