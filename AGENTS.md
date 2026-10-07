# FreeformHelper Agent Rules

## 任務使命與依據

本規則適用於本庫所有重構與 UI 變更。FreeformHelper 是單一 context repository；探索程式碼前，依 `docs/agents/domain.md` 讀取依賴圖、適用的 reference contract 與目前的 1.3.x roadmap。任務開始時必須先查 `docs/generated/project-dependency-graph.md`；缺失或過期時以 `scripts/build/generate-dependency-graph.ps1` 重建，再做深入檔案搜尋。

規格與可執行工作以 GitHub Issues 追蹤；commit 與 PR 必須連結相關 issue，依 `docs/agents/issue-tracker.md` 辦理。生命週期標籤使用 `needs-triage`、`needs-info`、`ready-for-agent`、`ready-for-human`、`wontfix`，詳見 `docs/agents/triage-labels.md`。

## 任務範圍與自主執行

- 預設以中文回覆，除非使用者另有要求。
- 採低用量執行：一次處理一個 TODO slice；除非里程碑或失敗調查需要，不做全庫掃描或全套測試。
- 不得以 `SuppressMessage`、`#pragma warning disable`、editorconfig severity 降級或同等方式壓制警告，除非使用者明確要求。
- 現有 lint 標準不得放寬；仍有 lint 失敗時不得合併。

## 委派與交接

多人工作或跨 session 交接時，交接紀錄格式與 bug ledger 入口見 `docs/handoff/README.md`。重構交接與實作步驟另見 `docs/guides/refactor-playbook.md`。

## 標準命令與工作區準備

- 每個邏輯里程碑執行 `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`，並推送分支。
- 每次 UI build、test 或 lint 循環前，先執行一次 `./scripts/dev/prepare-ui-workspace.ps1`。它會停止過期的 `FreeformHelper.UI`、spinner 與 dotnet UI 行程，並將修改的文字檔正規化為 CRLF；不要反覆用臨時 PowerShell 命令執行同樣的工作區整理。1.2 UI 階段曾因行程鎖定失去至少兩輪 build/test，且多次耗費時間修復行尾。
- 腳本修改或批次重寫後、build/lint 前，執行 `./scripts/dev/prepare-ui-workspace.ps1 -SkipStopApp`，將觸及的文字檔恢復為 CRLF，不依賴之後的格式化補救。所有 tracked 文字檔均須遵守 `.editorconfig` 與 `.gitattributes` 的 CRLF 規範。
- 若工具回報檔案被 `FreeformHelper.UI` 鎖定，執行一次 `./scripts/dev/prepare-ui-workspace.ps1 -SkipNormalizeLineEndings` 後再重試；不要直接重跑同一 build/test 命令。
- 每次 commit 前執行 `./scripts/tests/lint.ps1 -UseNoAppHost`；合併至 trunk（`1.3.x`）或 `main` 前執行 `./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost`（原規則稱合併至 `master` 前）。
- Repo verifier 的 structure、build、test 與 all lanes 由 `./scripts/verify.ps1` 執行；選擇與實際工作相符的 lane，詳見 `CONTRIBUTING.md`。
- `example/` 是私有 git submodule。新 clone 或 worktree 須在任何 gate 前執行 `git submodule update --init example`。缺少它時，example-data 測試可能略過，`run-refactor-gate.ps1` 會失敗；不得將其內容帶入本 repo、PR、issue 或 log。切換到 submodule 設立前的分支，先執行 `git submodule deinit -f example`。

## 核心架構與既有行為契約

- 使用者可見動作（command、property-change side effect、UI event）優先採單一入口。
- 使用者可見的計算結果，維持唯一的 source-of-truth 計算路徑與結果模型。多個 UI 入口只有在收斂到相同 state 與相同最終 predicate／計算路徑時才可保留。
- UI、RuntimeQuery、export、inspector、pad info 與 views 不得根據部分資料各自重新推導相同結果；多處顯示同一結果時應投影共用模型。
- ViewModel 僅負責 UI state 與 command binding；workflow 經由專責 UseCase／service。Views、Controls 與 ViewModels 不重複實作業務邏輯；共用邏輯抽至 Application 或 UI service。
- 明確列出 selection clear、rebuild trigger、fit/zoom reset 等 side effect，並將政策集中在同一處。
- 純邏輯與演算法放在 `src/FreeformHelper.Application`；UI workflow orchestration 放在 `src/FreeformHelper.UI/Services` 或專責 UseCases 目錄；UI 元件與樣式放在 `src/FreeformHelper.UI/Views`、`src/FreeformHelper.UI/Controls`、`src/FreeformHelper.UI/Styles`。

### 目前執行期機制

以下四項執行期機制，沒有明確任務不得變更。

- App settings persistence：`Load Project` 後，app-level general settings 進入 deferred mode；只有下一次成功 `Save Project` 後才 flush app-level writes。
- App settings 測試不得寫入使用者真實設定；`tests/FreeformHelper.Tests/TestInfrastructure/TestAppSettingsIsolation.cs` 會重新導向 `FREEFORMHELPER_APP_GENERAL_SETTINGS_PATH`。
- `Ctrl+S` 在非文字輸入情境呼叫 `SaveProjectAsync()`；儲存結果由 `MainWindow` 顯示頂部 toast。
- Runtime query IPC command 須符合 `docs/reference/runtime-cli-plan.md`；新增 query command 必須經由 `RuntimeQueryUseCase` 的單一入口。

## UI 樣式與主題規則

- Views／Controls 不新增 inline 顏色或尺寸，改用 `src/FreeformHelper.UI/Styles/Tokens.axaml` 的設計 token。Code-behind 不寫死顏色、尺寸或 brush，應從 token／resource 解析。
- Icon 遵守共用尺寸與 brush 慣例，不使用臨時 glyph 尺寸或自訂 stroke 顏色。
- 新 control style 或 icon 調整先在 Dev page 預覽，再廣泛套用；UI/UX 變更須確認 Dev page 預覽與 token 使用。
- Advanced edit 區域必須使用平坦的 accordion／header-body 處理，不恢復舊式 dropdown 外觀、巢狀 card 加預設 expander 的組合。
- 新 UI resource 必須透過 `DynamicResource` 與 token 支援 theme；缺少 token 時先加入 `Tokens.axaml`，不得在 XAML 寫死值。

## 風險與驗證關卡

- Runtime-only style 風險在 commit 前必須有 guard test；tooltip style 變更同時需要 `UiLayoutGuardTests` 的 static style guard 與 headless tooltip-open smoke test。
- 資料路徑、golden 與 1.3.x gate 的細節依 `docs/reference/refactor-contract-1.3.x.md`。

## 分支與審查邊界

- The default branch is `1.3.x` (trunk). `main` currently equals the initial import commit and will hold released versions only. Work uses `feature/<version>/<topic>` branches targeting `1.3.x`.
- Changes to `AGENTS.md` or `CONTRIBUTING.md` require owner confirmation in chat before editing. The owner said on 2026-10-04: 「讓我在聊天中確認即可」 ("Confirming in the chat is enough"). A GitHub review is not required for this confirmation; the owner-approval rules for high-risk PRs in `CONTRIBUTING.md`, including `src/**`, `scripts/**`, and `.github/**`, continue to apply.

- Commit 範圍須小而明確，每個 commit 含 title 與 body；不把無關修正放在同一 commit，優先逐一提交。
- 分支、版本與發佈治理見 `docs/governance/branch-version-and-release-governance.md`；PR 與合併規則、貢獻者執行順序見 `CONTRIBUTING.md`。

## Skills、掃描與完成條件

- 符合觸發條件時，必須遵循本庫自動探索的 `.agents/skills/freeform-refactor/SKILL.md`、`.agents/skills/ui-consistency/SKILL.md` 與 `.agents/skills/repo-optimizer-loop/SKILL.md` 的流程。
- 全庫重構掃描至少包含檔案大小／行數熱點、analyzer 警告摘要，以及 `docs/reference/behavior-inventory.md`、`docs/guides/settings-entry-matrix.md` 的過期文件／行為檢查。
- 全庫掃描須明確稽核「同一 feature／result 的多條推導路徑」，分開記錄可接受的多入口單一路徑與不可接受的多路徑再推導。
- 新發現的最佳化／重構項目，立即寫入 `ROADMAP.md` 對應的線(修正線、產品功能、整合 Core),附目標版本、狀態與 PR 或 issue 連結。
