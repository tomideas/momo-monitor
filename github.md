# GitHub 資訊 · GitHub Info

> 專案：Momo System Monitor · Momo 系統監測
> 建立：2026-09-14

## 仓库信息 · Repository

| 項目 | 值 |
|------|-----|
| 远端 · Remote | https://github.com/tomideas/momo-monitor.git |
| 分支 · Branch | `main` |
| 授权 · License | GPL-3.0 |
| 可见性 · Visibility | Public |
| 本地路径 · Local Path | /Volumes/ssd/temp/@coding/itx/tools/status |

## 凭据 · Credentials

- username: `tomideas`
- PAT（Personal Access Token）：`<paste-new-ghp-here>`
  - 在 https://github.com/settings/tokens 管理（創建／作廢／更換）
  - **不要把真实 PAT 写进本文件**，只留占位槽

## Push 流程 · Push Workflow

临时挂 credential helper，push 完立即 unset（不留痕跡）：

```bash
git config --local credential.helper '!f() { echo username=tomideas; echo password=<PAT>; }; f'
git push
git config --local --unset credential.helper
```

验证：`git status --short --branch` 应显示 `main == origin/main`。

## 版本规则 · Versioning（沿用 momo-illustrator 格式）

1. 每次程式碼更新必須同步：
   - `StatusMonitor/StatusMonitor.csproj` 的 `<Version>`
   - `CHANGELOG.md` 頂部新增 `## [新版本] — YYYY-MM-DD`（分類：新增 / 修復 / 改善）
2. 不允許只改程式碼不改版本號和 changelog。

## 当前状态 · Current Status

- 最新版本：`0.1.0`（csproj 中定義；0.1.1 僅為授權文件，未升號）
- 最近 commits：

| Commit | 說明 |
|--------|------|
| `9f9a4e4` | chore: untrack design-system, add to .gitignore |
| `7dcac2d` | docs(license): add GPL-3.0 LICENSE |
| `a2cddcd` | feat: v0.1.0 — Momo System Monitor initial release |

## 仓库内容约定 · Repo Conventions

- **不收**（已 gitignore）：`*.exe`（構建產物 build artifacts）、`bin/` `obj/`、`momo-data/`（使用者個人用電資料）、`design-system/`（本地設計工具）
- **保留**：`StatusMonitor/libs/`（自建 LibreHardwareMonitor DLL，建置必需）
