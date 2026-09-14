# 项目设计系统

App 与直接打开 HTML 共用版面和设计数据：App 读取 JSON，file:// 页读取保存时自动生成的 preview-data.js。预览会每三秒检查快照。JSON 经 AI 离线修改后，必须运行 Momo Design System.exe --sync-only（或打开 App）生成快照与 DESIGN.md，才能保证本地 HTML 是最新版本。快照不要手动修改；本地 HTML 只预览，修改和保存请使用 App。

把整个 design-system 文件夹放到项目根目录。Windows 用户打开 `../Momo Design System.exe`，即可直接编辑和保存，不用安装 Node.js。每个项目独立运行，自动选择空闲端口，关闭窗口即停止本项目服务。详情见 [桌面版说明](DESKTOP.md)。macOS 将采用系统 WKWebView，但可执行版仍需在 Mac 上构建、签署和实机验收。

原本的 Chrome / index.html 入口继续保留。App 优先使用项目内的 HTML 与脚本，AI 更新编辑页后重新载入即可，不需要重新打包外壳。

JSON 是唯一设计资料；保存或 App 发现 AI 回写时自动生成 DESIGN.md。在「更多 → 导出 DESIGN.md」可取得已保存版本。不要分别维护两份；App 关闭时的同步方式见 AI.md。

第一次使用请看 [新手教程](tutorial.html)。顶部「复制给 AI」会直接显示完整提示词，右上角的复制图标负责复制；弹窗左下角可随时打开完整教程。

当前界面：顶部只保留主要操作「复制给 AI」与「更多 ⋯」。保存状态只在发生修改、保存、冲突或错误时出现，不显示 AI 在线状态。教程固定在侧栏，也可从「更多 → 设置」重新启用启动提示。完整流程审查与待改善事项见 [易用性检查](USABILITY.md)。

## 日常流程

1. **交给 AI**：AI 在当前项目查找已有的 `design-system/`，读取 `design-system/AI.md` 与 `design-system/design/design-system.json`。找不到或有多份时先询问，不新建、搬迁或复制。
2. **确认方向**：项目已有 UI 时，AI 先让用户选择「规范更新项目」或「项目更新规范」，避免覆盖错误的一方。
3. **调整并保存**：点击样本修改，主题选择器可切换覆盖值，点击保存写入当前项目 design-system.json。未保存的值只在浏览器草稿中。
4. **套用到软件**：复制提示词给 AI。AI 直接读取最新 JSON、修改对应源码并验证。EXE 项目还需要构建后才能体现改动。
5. **持续开发**：新页面先读最新设计系统；公共组件和规则变化时同步维护映射。
6. **从代码更新**：软件 UI 在其他任务中发生变化后，告诉 AI 按当前项目更新规范。AI 比较现状和已保存设计，保护尚未应用的用户修改。

## 状态含义

- 规范已保存：JSON 已写入项目，不表示产品界面已经改变。
- 已应用到项目：必须由 AI 修改源码、构建并检查实际界面后才成立；App 不追踪 AI 是否在线。

## 复制到其他项目

工具文件可复用；项目数据属于各自项目。template.json 保留通用起步值。AI 发现名称、sourceEntry 或源码映射属于旧项目时，先备份旧数据到 backups/，再从模板提取新项目。不能把「已在 Momo 使用」当成新项目也已完成接入。

保留未知字段和已有规范，不能覆盖项目现有 AGENTS.md / CLAUDE.md。若已有同名设计系统，比较合并。多个候选项目无法判断时才询问。

## 运行与限制

独立运行：`node design-system/design/server.mjs`。需要已有 Node.js；默认端口 4173，也可用 `DESIGN_SYSTEM_PORT=0` 自动分配。服务只负责 App 打开期间的保存，不是 AI 的连接服务；AI 可在 App 关闭时直接读写 JSON。

也可由已有 Node 服务挂载 `createDesignSystemHandler({ directory })`。本机网址和端口属于 App 内部实现，不写入给 AI 的提示词或项目规范。

浏览器不能通过普通本地 HTML 静默写项目文件，所以 file:// 页是接入入口，HTTP 页负责保存。依据：[MDN 同源策略](https://developer.mozilla.org/en-US/docs/Web/Security/Defenses/Same-origin_policy)。服务保留同源写入与版本冲突保护，不开启跨站写入。

有源码才能建立可靠设计映射；只有 EXE 时截图可作视觉参考，但不足以还原所有组件状态。通用网页样本也不等同于 WPF 或其他原生控件的实际渲染。

## 文件

- AI.md / CONNECT.md：AI 工作协议与首次接入流程。
- design-system.json：当前项目唯一可编辑设计数据；template.json：新项目起点。
- index.html / app.js：编辑器；editor-link.js：机器生成的本地入口，不是项目事实。
- server.mjs / server.test.mjs：保存服务与接口验证。
- components/ / guidelines/：项目样本及规则；ASSESSMENT.md：本次评估与取舍。

验证：`node --test design-system/design/server.test.mjs design-system/design/workflow.test.mjs design-system/design/document.test.mjs`。


