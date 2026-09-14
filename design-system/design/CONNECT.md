# 使用当前项目设计系统

`design-system/` 随项目保存；AI 通过项目文件使用它，不需要连接 App，也不依赖 localhost 地址或固定端口。

1. 在当前项目内定位已有的 `design-system/`。读取 `design-system/AI.md` 和最新 `design-system/design/design-system.json`。不要新建、搬迁或复制另一份；找不到或有多份无法确定时先问用户。
2. 核对 `project.sourceEntry`、项目名称及 `definitions.*.mapping`。如果设计系统来自另一个项目，先备份旧 JSON，再基于 template.json 建立当前项目资料，不沿用旧项目的名称、来源或应用记录。
3. 如果项目已有 UI，而用户尚未明确方向，先询问用户选择：
   - 使用 design-system 更新项目 UI；
   - 按当前项目 UI 更新 design-system。
4. 应用设计时，优先使用用户保存的规范，保留未映射参数的限制；修改源码后完成构建与真实界面检查，并回写本次实际改变的共享设计。
5. 从项目更新设计系统时，比较当前源码与 JSON，保留尚未应用的用户调整；冲突先说明，不盲目覆盖。此次不修改产品 UI。
6. 首次采用时，在项目原有 AGENTS.md 与 CLAUDE.md 加入简短入口：UI 任务先读 `design-system/AI.md` 和最新 JSON。保留已有规则，已有等价入口时不重复。
7. AI 直接原子写入 JSON。Windows 可运行 `design-system/Momo Design System.exe --sync-only` 生成 DESIGN.md 与预览快照；没有运行时，它们会在用户下次打开 App 时自动生成。

App 只供用户查看、调整和保存设计。端口属于 App 内部实现，不放进提示词，不作为 AI 使用设计系统的前提。
