# 開發執行流程

狀態：本庫現行開發流程。任務授權與技術規則以根目錄 `AGENTS.md` 為入口；版本與分支見 [分支治理](branch-version-and-release-governance.md)。實際 gate 以本庫腳本和 1.3.x roadmap 為準。

## 開始前（Preflight）

1. 檢查目前分支與工作樹。讀 `docs/generated/project-dependency-graph.md`，再依 `docs/agents/domain.md` 讀 `TODO.md`、active roadmap 與適用 reference contract。
2. 確認一個 TODO slice／issue 的成果、影響層、side effects、驗收條件、targeted test 和 human／golden gate。`S15.*` 的 issue-link 例外見 `TODO.md S15.002`。
3. UI build/test/lint 前執行 `./scripts/dev/prepare-ui-workspace.ps1`；新 worktree 在 gate 前初始化私有 `example/` submodule。不得複製其內容到記錄或公開檔案。

## 工作准入（Admission）

重構時先列出使用者動作的 entry、reader、writer 與 side effects，確認相同結果的每個 consumer 是否共用一條計算路徑與一個結果模型。多入口收斂到單一路徑可以保留；多處從 partial data 重算相同結果依 playbook 處理。純邏輯、UI orchestration、ViewModel 和 View／Control 的放置依 `AGENTS.md`。

PR 需記錄關聯 issue／TODO ID、成果與範圍、行為／契約影響、實際驗證及 golden 證據（若適用）。`S15.005c` 尚未建立範本的 authority classifier、CODEOWNERS 和 review record，因此此處不宣稱它們已生效。

## 驗證選擇（Narrow test selection）

| 變更 | 首要驗證 |
| --- | --- |
| 文件與治理結構 | 檢查 diff、連結與 CRLF；執行 `./scripts/verify.ps1 -StructureOnly`。 |
| UI／樣式 | Dev page 預覽、token 檢查、`UiLayoutGuardTests` 與適用 headless smoke；tooltip 同時需要 static style guard 和 tooltip-open smoke。 |
| 1.3.x 程式 slice | UI build、targeted tests、lint，並按 roadmap 的 G2～G5 風險選擇。 |
| 合併 1.3.x milestone | 全檔 lint、refactor gate 與 UI snapshots（roadmap G6）。 |

`./scripts/verify.ps1` 的 `-StructureOnly`、`-CiLane build`、`-CiLane test -Shard core|ui|viewmodel|snapshots` 與 `-All` 是現行 repo verifier lanes。`viewmodel` shard 暫時不阻擋 CI 合併，依 `TODO.md S15.002` 關閉條件恢復。`example/` 缺失造成的略過不是私有資料 gate 的成功證據。

## 審查與 checkpoint

每個邏輯 slice 先完成直接相關的測試、build 與 lint，檢視待提交的 diff，再以單一範圍的 commit 記錄結果。每個里程碑推送分支；合併條件依 [貢獻指南的「合併邊界」](../../CONTRIBUTING.md#合併邊界owner-決定2026-10-02)。未通過的 gate 不視為通過。

## 規格一致性與重試

對照受影響的 reference contract、現行行為和測試；若矛盾涉及 golden 或產品行為，列出證據與人員 gate，不自行改寫期待值。失敗時先判斷是否來自本次變更、既有問題或環境；重複的阻塞應記錄並縮小調查範圍。全庫掃描的固定盤點與 TODO 同步規則見 `AGENTS.md`。

## 交接

完成的文件工作記錄變更原因、實際驗證與限制。跨 session 或多人工作依 [handoff protocol](../handoff/README.md) 記錄工作範圍、branch／head、檢查與剩餘 gate；不要在交接紀錄複製私有資料。
