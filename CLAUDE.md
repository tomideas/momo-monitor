# UI 设计规范

## 设计系统位置

设计系统纳入 Git，正式位置是当前检出的 `design-system/`。涉及 UI、主题、组件或新增页面时，先读取其中的 `AI.md` 和最新 `design-system.json`。旧 worktree 若尚未取得该目录，临时读取主检出 `D:\temp\@coding\itx\tools\status\design-system\`；不得据此判断项目没有设计系统，也不得另找模板代替。

`design-system/design/guidelines/defaults.json` 只用于用户明确要求的「恢复默认」。其他任务不得读取、比较或用它覆盖正式 JSON。`design-system/design-system.json` 是唯一正式设计资料；`design-system.md` 是自动生成文件，不独立修改。

## 两种方向

一般产品 UI 工作默认属于「用设计系统更新项目」：使用 JSON 中用户已保存的设计，按 mapping 应用；没有 mapping 时自行寻找合理的公共实现位置，成功后补回 source 和 mapping。找不到安全位置时保留该设计值并报告，不能用现有源码值覆盖它。

只有用户要求「用项目更新设计系统」、同步现况或整理真实设计时，才执行反向同步：不修改产品源码；源码已经实现的参数必须以运行时实际生效值更新 JSON，只有源码确实未实现、没有等价语义位置的参数才保留为未来设计。不得把 JSON 与源码不同自动解释成「尚未应用」而跳过同步，也不得要求用户逐项决定覆盖、命名或 mapping。

本项目为 WPF。修改产品 UI 后，在同一任务回写涉及的共享设计，并验证源码、构建及相关原生界面后才报告已应用。编辑器 App 自身的界面样式不属于 Momo 产品设计，不回写。

