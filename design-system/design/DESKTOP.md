# 独立桌面版

用户双击 design-system 根目录的 Momo Design System.exe。不要进入 app/ 找启动文件；不要单独复制 EXE。

```
design-system/
├─ Momo Design System.exe       用户入口
├─ AI.md                   AI 快速入口
├─ design/                 本项目的设计资料与 HTML
│  ├─ index.html
│  ├─ design-system.json   唯一正式资料
│  ├─ DESIGN.md            自动生成
│  ├─ components/
│  └─ guidelines/
├─ app/windows/            Windows 运行文件（约 1 MB）
└─ node_modules/           开发资料
   └─ native-source/       原生宿主源码、图标与构建脚本
```

## 日常使用

打开 App → 修改 → 保存 → 复制说明给 AI。新手教程在 App 侧栏。每个项目使用自己的完整文件夹，双开自动使用不同端口；同一项目再次打开会聚焦现有视窗。

关闭主视窗会停止本项目服务。有未保存调整时，可返回保存或保留本机草稿退出。草稿按项目路径隔离，搬到其他电脑或位置前先保存。本次从旧结构迁移会保留原项目的 App 草稿识别；Chrome 草稿与 App 草稿仍独立。

更多 → 设置：可以重新打开新手教程、选择启动时是否显示教程，以及调整连接端口。端口 0 表示自动；指定端口下次启动生效，被占用时可选择其他端口，不停止其他软件。

JSON 变化会更新预览和 DESIGN.md。HTML 是编辑画面，不是第二份数据；Markdown 是派生文件，不独立修改。AI 应先读根目录 AI.md 与最新 JSON，修改产品后回写相关共享设计，保留其他未应用修改。更多 → 导出 DESIGN.md 可取得快照。

直接打开 design/index.html 时读取自动生成的 preview-data.js，使用与 App 相同的色块和排版。保存同时生成 Markdown 和预览快照，两者带同一 designRevision。App 关闭期间的 JSON 修改需要 --sync-only 或下次打开 App 才会刷新快照；不能仅靠静态 HTML 自动监听 JSON。

App 打开期间每两秒检查 JSON；运行地址保存在 design/.desktop-runtime.json，必须 GET 并核对 file 才可使用。App 关闭时可用根目录 Momo Design System.exe --sync-only 生成 Markdown，或下次打开时生成。

升级程序时保留 design/。复用到其他项目请使用通用起步套件，其中不包含 Momo 项目数据。

## 开发

Windows 宿主改用 WinForms + 系统 WebView2 Evergreen Runtime，不再随项目附带 Electron/Chromium。源码、图标和构建脚本位于 `node_modules/native-source/windows/`；运行 `build.ps1` 会以 `app/windows/` 现有的 WebView2 桥接库编译宿主及根目录入口。App 始终加载 `design/` 的 HTML 和脚本，修改网页后不用重建宿主。

正式 Windows 文件只有根目录入口，以及 `app/windows/` 内的宿主、WebView2 .NET 桥接和 Loader，总计约 1 MB。用户机器需要 Microsoft Edge WebView2 Runtime；Windows 10/11 通常已经安装。Windows 初版尚未代码签署。

macOS 版将复用同一 `design/` 和 API 合约，并采用系统 WKWebView，避免携带 Chromium；架构说明在 `node_modules/native-source/macos/`。macOS 二进制、签署与实机验收尚未交付。

验证：在项目根目录运行 node --test design-system/design/server.test.mjs design-system/design/workflow.test.mjs design-system/design/document.test.mjs。

