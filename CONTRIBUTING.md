# 貢獻指南

本指南彙整本庫現行的分支、提交、驗證與合併規則。實作細節依 [開發執行流程](docs/governance/development-workflow.md)，版本與分支邊界依 [分支、版本與發佈治理](docs/governance/branch-version-and-release-governance.md)。

## 分支模型

- The default branch is `1.0.x` (trunk). `main` currently equals the initial import commit and will hold released versions only.
- Work uses `feature/<version>/<topic>` branches, such as `feature/1.0.x/<topic>`, targeting `1.0.x`.
- 1.0.x 的版本順序、目標版本與狀態以 `ROADMAP.md` 為準；slice 規格、gate 與退出條件以 `docs/reference/refactor-contract.md` 為準。發佈 workflow 尚屬 `S15.005e`，不得把範本的發佈程序視為本庫已實作的 gate。

## 變更、commit 與 PR

1. 從 `ROADMAP.md` 或現有 issue 確定一個可驗收的範圍；功能或範圍變更時立即同步 `ROADMAP.md`。先依 `AGENTS.md` 讀取依賴圖及適用契約。
2. 一次完成一個邏輯 slice；程式與 UI 變更依 `docs/guides/refactor-playbook.md` 與 1.0.x roadmap 的 gate 驗證。
3. 每個 commit 保持單一邏輯範圍，使用 title 與說明原因、結果及驗證的 body。依 `docs/agents/issue-tracker.md` 使用 `Refs #N`；`S15.*` 的既有例外使用獨立一行 `Refs: ROADMAP.md S15.xxx`，直到可建立對應 issue。
4. PR 說明列出關聯 issue／TODO ID、行為或契約影響、實際驗證命令與結果及必要的 golden 證據。

## 驗證入口

`./scripts/verify.ps1` 是 structure、build 與 test 的單一入口；實際 lane 與 shard 內容由腳本決定，CI workflow 只命名它們。

| 命令 | 用途 |
| --- | --- |
| `./scripts/verify.ps1 -StructureOnly` | 必要檔案、submodule link、私有路徑、SDK pin、action pin、測試分組、CRLF、XAML action role。 |
| `./scripts/verify.ps1 -CiLane build` | 全檔 lint、analyzer 與 UI build，warning 視為 error。 |
| `./scripts/verify.ps1 -CiLane test -Shard core` | Core test shard；`ui`、`viewmodel`、`snapshots` 可取代 `core`。 |
| `./scripts/verify.ps1 -All` | Structure、build 與所有 test shards。 |

每個工作里程碑仍依 `AGENTS.md` 執行 UI build；每次 commit 前跑 `./scripts/tests/lint.ps1 -UseNoAppHost`，合併至 trunk（`1.0.x`）或 `main` 前跑 `./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost`（原規則稱合併至 `master` 前）。1.0.x 的 G0～G6 與 targeted suites 見 `docs/reference/refactor-contract.md`。現行 CI 的 required checks 是 `policy / structure` 與 `dotnet / build-test`；`viewmodel` shard 依 `ROADMAP.md S15.002` 的暫時決定報告但不阻擋合併，關閉該項後恢復必過。

`example/` 是私有 git submodule。新 clone 或 worktree 須在任何 gate 前執行 `git submodule update --init example`。缺少它時，example-data 測試可能略過，`run-refactor-gate.ps1` 會失敗；不得將其內容帶入本 repo、PR、issue 或 log。切換到 submodule 設立前的分支，先執行 `git submodule deinit -f example`。

## 合併邊界（Owner 決定，2026-10-02）

PR 只要觸及以下任一高風險範圍，整個 PR 就需要 owner 在 GitHub 核准：production code `src/**`、CI workflow `.github/**`、決定 gate 內容的 `scripts/**`、`.editorconfig`、`Directory.Build.props`、`Directory.Packages.props`、`global.json`、發佈相關檔案，以及 agent 權限設定。高風險 PR 仍建議取得本專案的獨立審查。其餘文件與測試變更，在本專案派出的獨立審查結論為 `accept`、未留 P0／P1、且 required checks 綠燈後由 bot 合併。範本專案的 session 不替本專案審查。

The bot is the repository's existing GitHub App, inherited from the template project. Changes to `AGENTS.md` or `CONTRIBUTING.md` require owner confirmation in chat before editing. The owner said on 2026-10-04: 「讓我在聊天中確認即可」 ("Confirming in the chat is enough"). A GitHub review is not required for this confirmation; the GitHub owner-approval rules for `src/**`, `scripts/**`, `.github/**`, and the other high-risk paths above continue to apply.

合併前再次確認 PR 的目前 head、適用的獨立審查與 owner 核准，以及 required checks。使用 `gh pr merge <n> --merge --match-head-commit <head>`，以當下確認的 head 作為合併邊界。

Execution status for the S15.005c authority policy/review record and the S15.005d ruleset is maintained in ROADMAP.md; do not describe unimplemented automatic checks as current gates.
