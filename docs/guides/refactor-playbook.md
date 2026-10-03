# FreeformHelper Refactor Playbook (for AI Agents)
最後更新：2026-08-08

## 目標
這份文件提供「可直接照做」的重構流程，讓不同 AI/開發者在交接時能維持一致品質：
- 行為一致（single-entry、明確 side effects）
- 提交可回溯（小步驟、單一邏輯 commit）
- 驗證可重現（固定 build/test/基線流程）

## 入口與依賴文件
1. `TODO.md`
2. `docs/README.md`
3. `docs/core/workflow-pipeline.md`
4. `docs/core/notch-validation-flow.md`（若動到 Step6 驗證/Notch row）
5. `.agents/skills/freeform-refactor/SKILL.md`
6. `.agents/skills/ui-consistency/SKILL.md`
7. `docs/guides/ui-density-token-rules.md`（動到 UI layout/style 時）

## Repo 內 Skill 自動掃描位置
- Codex 會掃描 repo 下的 `.agents/skills/<skill-name>/SKILL.md`。

## 分支與提交流程
1. **先確認分支**：避免直接在 `master/main` 修改。
2. **一個里程碑一個 commit**：不要混入不相關修正。
3. 每個里程碑都執行：
   - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
   - 必要的 targeted test（例如 workflow/快照/匯出）
4. 通過後才 push。
5. PR 前套用：
   - `docs/guides/refactor-pr-checklist.md`
   - 最新 repo scan 需可對照 `docs/guides/repo-refactor-scan-template.md`

## 標準重構循環（每一輪都一樣）
1. **Inventory**
   - 找出目前多入口：Command、PropertyChanged、View event、CLI。
2. **Unique Path Audit**
   - 分清楚「多入口但最後收斂到同一路徑」與「同一結果被多個地方各自再算一次」。
   - 前者可接受；後者必須列為缺陷或重構項目。
3. **Single Entry / Single Result 設計**
   - 決定唯一入口（通常在 `UseCase` 或 `UI/Services`）。
   - 決定唯一結果模型：同一個 user-visible 結果只能有一個 source-of-truth model。
4. **Side Effects 明文化**
   - 明確列出：selection clear、downstream invalidation、focus/step jump、status text。
5. **實作**
   - ViewModel 只保留 UI state/binding；邏輯搬到 service/usecase。
6. **驗證**
   - Build + targeted tests。
7. **文件同步**
   - 更新 `TODO.md` 狀態與必要 spec/README 入口。
8. **提交**
   - `type(scope): subject` + 清楚 body（改了什麼、為何這樣做）。

## Single Entry vs Single Result（必須分開檢查）
- `Single Entry`：同一個使用者動作應從唯一入口進來，例如 `SaveProjectAsync()`、`RuntimeQueryUseCase`。
- `Single Result`：同一個使用者可見結果應只由一個核心計算路徑產生，其他地方只能讀取/投影，不能再推導。
- 合理：
  - 多個按鈕/快捷鍵都呼叫同一 command。
  - 多個 UI panel 都讀同一個 snapshot/result model。
- 不合理：
  - UI / RuntimeQuery / export / inspector 各自根據 partial data 重算 `ToFull enabled`、`Stage3Area`、`CombinedRatio`。
  - 多個 formatter / ViewModel 各自組同一組業務結論字串，導致顯示漂移。

## Unique Path 掃描清單（repo scan 必做）
1. 找出同一 feature/result 的所有入口與所有 reader。
2. 確認是否存在 second-pass derivation：
   - UI 自己再算一次
   - RuntimeQuery 自己再算一次
   - export 自己再算一次
   - inspector/pad info 再從 partial data 拼一次
3. 若存在 second-pass derivation：
   - 先把它寫入 `TODO.md`
   - 再定義 single source-of-truth model
   - 最後補 regression tests，鎖住 reader 間一致性。

## Direct State Mutation 掃描清單（repo scan 必做）
1. 找出 public mutable collection / 外部可直接 mutation 的 state model。
2. 對每個 hotspot 標註 convergence route：
   - private mutable backing + read-only facade
   - clone-on-set
   - dedicated replace/apply API
3. 若本輪不處理，必須在 scan report 列出 owner + target milestone。
4. PR 前必須逐項勾選 `docs/guides/refactor-pr-checklist.md` 的 S11.52 gate。

## 資料結構變更等價性（必守）
- 任何資料結構調整（欄位改名/型別化/模型拆分）都必須附「前後結果無差別」證明。
- 最低要求：
  - 同一組輸入，輸出 `NotchTableRow.Values` 保持一致（或有明確遷移規則）。
  - Export 結果（CSV/C initializer）前後等價。
  - Validation / Runtime Query 使用者可見結果前後等價。
- 建議做法：
  - 加一組 typed vs legacy 的等價性測試（同輸入、同排序、同結果）。
  - 若有 fallback 路徑，測試必須同時覆蓋 primary + fallback。

## 專案規範（重點）
### 架構
- Pure logic：`src/FreeformHelper.Application`
- UI orchestration：`src/FreeformHelper.UI/Services`
- ViewModel：只保留狀態與命令綁定，避免商業邏輯分散

### UI/樣式
- 禁止 inline color/size，改用 `src/FreeformHelper.UI/Styles/Tokens.axaml`
- 新資源需 theme-aware（`DynamicResource`）
- icon 尺寸/筆刷遵守共用規範，不做 ad-hoc

### 行尾/檔案格式（必守）
- repo 文字檔以 `CRLF` 為標準，依 `.editorconfig` / `.gitattributes` 執行。
- 用 script、`apply_patch`、或外部工具改檔後，若 touched files 出現 `ENDOFLINE` 問題，應在提交前先把該批檔案正規化回 `CRLF`。
- 不要把 line-ending 修復留給後續 lint stage 才處理；這會讓 analyzer/build gate 因非邏輯問題失敗。

### Workflow（Step1~Step5）
- Step 依賴、invalidations、focus 規則應集中到 pipeline service
- 禁止在多個檔案手刻同一套「Invalidate + Move」邏輯

### 設定層級（必守）
- 一律遵守：`Project（完整設定快照） > App General（白名單） > Default（程式預設）`
- `Project Save/Load` 仍保留完整 `ProjectSettings + ProjectUiSnapshot` roundtrip，不可裁剪。
- `App General` 僅儲存白名單：
  - `UiViewSnapshot`
  - `UiImportSnapshot`
  - `Behavior.ApplyVisualPreferencesOnProjectLoad`
- `Load project` 行為：
  - 幾何/流程設定永遠以 project 為準。
  - 視覺層是否覆蓋由 `ApplyVisualPreferencesOnProjectLoad` 決定（預設 ON）。
  - 載入後 app-level 寫入進入 deferred 模式，僅在下一次 `Save Project` 成功時 flush（避免中途覆蓋 app settings）。

### 設定入口職責（M11 收斂）
- `WorkspaceHeader` Display popup：只放高頻、即時視覺調整（直接 TwoWay）。
- `SettingsWindow`：operator-facing 必要設定入口（General + Step1~5），以 draft/apply 管理；完整 persistence schema 不代表每個欄位都必須有 normal-flow editor。
- `RightWorkflowPanel`：workflow 操作 + General 快捷設定 + deep link。
  - `General Settings` 保留常用欄位直接編輯（Grid / AA / source / alignment）。
  - Step 區塊只保留正常執行確實需要的人工決策與必要快捷（例如 Step3 stage/autoplay）；automatic policy、calibration/scoring、derived、diagnostic、compatibility 欄位不得為了完整 schema 而排進 numbered flow。
- `CoordinatePixelWidth/Height` 與 Step4 mapping weights/candidate/confidence/ambiguous thresholds 不屬於 normal operator flow；遷移 UI 時先隱藏／分層，保留 defaults、consumer 與 project roundtrip，不在同一 slice 刪 schema。
- 入口矩陣與 single-source 對照，統一以 `docs/guides/settings-entry-matrix.md` 維護。

## 測試分類建議（執行順序）
1. **快速邏輯測試**（Application/Workflow）
   - 先跑，回饋最快
2. **ViewModel/Service 整合測試**
   - 驗證 workflow side effects
3. **UI Snapshot 測試**
   - 僅在 UI 結構/樣式變更時更新 baseline

建議命令（範例）：
- `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug --filter FullyQualifiedName~WorkflowPipelineServiceTests`
- `./scripts/tests/update-ui-baseline.ps1 -Mode DryRun`
- `./scripts/tests/run-refactor-gate.ps1 -UseNoAppHost`（固定跑全部測試分組，含 `ui-snapshots`）

## 常見錯誤與防呆
1. **重構後行為不一致**
   - 原因：入口未收斂，仍有舊路徑繞過新 service。
   - 防呆：`rg` 搜尋舊 helper 呼叫點，逐一改為新入口。
2. **UI 看起來正常但規則漂移**
   - 原因：CLI/右側按鈕/快捷鍵走不同邏輯。
   - 防呆：讓 UI 與 CLI 共用同一方法。
3. **快照測試污染**
   - 原因：未區分 DryRun/Apply 或混入不相干 UI 變更。
   - 防呆：先 `DryRun`，確認差異只含本次目的再 `Apply`。

## 交接模板（貼到 PR 或 commit 說明）
1. 本輪目標：
2. 單一入口：
3. side effects 改動：
4. 驗證命令與結果：
5. TODO 狀態同步：
6. 下一輪建議：

