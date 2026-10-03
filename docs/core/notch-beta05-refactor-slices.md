# Beta 0.5 Notch Refactor Slices (Start Anchor)

最後更新：2026-04-19  
適用分支：`beta0.5`

## Refactor Start Marker
- Marker：`BETA05-NOTCH-REFACTOR-START-2026-04-19`
- 說明：後續 Beta 0.5 notch 流程重構 commit，都以此 marker 作為起點追蹤。

## Commit 記錄規範（後續切片必填）
每個重構 commit（S11.132 起）在 commit body 要包含以下欄位：

```text
Refactor-Start: BETA05-NOTCH-REFACTOR-START-2026-04-19
Refactor-Slice: S11.xxx
Refactor-Record: docs/core/notch-beta05-refactor-slices.md
Change-Items: item1; item2; item3
```

建議 commit subject 格式：

```text
refactor(beta0.5-S11.xxx): <slice summary>
```

## Slice 清單

### S11.132 Snapshot 收斂
- 範圍：
  - `WorkflowDataSnapshot` 建構與快取。
  - `FreeformHelperViewModel` 內 snapshot invalidation 來源收斂。
- 目標：
  - 同一操作批次只建一次 snapshot。
  - 降低 `ToFrozenDictionary` 重複成本。

### S11.133 Diff Identity Pipeline
- 範圍：
  - Raw diff / Visible diff / Projected row identity 的中間契約。
  - `NotchCurrentVisibleDiffProjectionService` 與上游流程介面。
- 目標：
  - 單一 diff 身分語意來源，避免多處二次推導。
  - 保留現有衝突 fallback 行為，但可追蹤、可測試。

### S11.134 Notch Generation Orchestration
- 範圍：
  - `NotchTableGenerator.Generation*`。
  - stage 切分（candidate -> select -> serialize -> compatibility projection）。
- 目標：
  - 不改數學輸出，只改流程責任邊界。
  - 提升可測試性與可解釋性。

### S11.135 Simulation Contract 收斂
- 範圍：
  - `NotchApplySimulationService` 對重複 diff key 的處理。
  - 與 notch row identity 合約對齊。
- 目標：
  - Simulation 由「補救式規則」轉為「契約消費者」。
  - duplicate diff 的來源與採用策略可在 diagnostics 清楚追蹤。

### S11.136 文件一致化
- 範圍：
  - `docs/core/notch-overall-flow-mermaid.md`
  - `docs/core/notch-v21-v22-flow.md` 與關聯 spec
- 目標：
  - 文件敘述與程式行為一致。
  - 明確分出 canonical path 與 compatibility path。

### S11.137 Simulation duplicate diff 聚合修正
- 範圍：
  - `NotchDiffIdentityPipeline.BuildActiveDiffBaseline(...)`
  - `NotchApplySimulationService` / `SimulationWorkspaceViewModel` duplicate diff 契約顯示
- 目標：
  - 將 duplicate diff baseline 由 keep-first 改為 merge-sum，避免 active surface 感應量遺失。
  - 保持 Step4 visible diff 指派邏輯不變，只修正 Simulation 聚合層。

### S11.138 Settings toggle 版型統一
- 範圍：
  - `SettingsGeneralSectionView.axaml`
  - `SettingsStep2SectionView.axaml`
  - `SettingsStep3SectionView.axaml`
  - `SettingsStep4SectionView.axaml`
  - `SettingsStep5SectionView.axaml`
- 目標：
  - 將主要布林選項統一為可掃讀的 switch row 版型，降低設定頁多重勾選項目的視覺負擔。
  - 不改設定邏輯與綁定語意，只做 UI 呈現一致化。

## 驗證基準
- 每個 slice 完成需至少執行：
  - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
  - 對應範圍測試（最小必要）
  - `./scripts/tests/lint.ps1 -UseNoAppHost`
- 若受執行中 UI 行程鎖檔影響，需在 commit 記錄明確註記 blocked 原因與後續補驗證計畫。

## Execution Log

### 2026-04-19 / S11.132 (Phase A)
- 範圍：
  - `FreeformHelperViewModel.Core` 導入 snapshot cache 與 invalidation。
  - Step1/Step4/SeeRegular/LoadProject 的來源資料更新路徑補上失效點。
- 變更重點：
  - `BuildWorkflowDataSnapshot()` 改為 cache-first。
  - 新增統一入口：
    - `SetLatestPadMatchResult(...)`
    - `SetLatestVisibleIndexAssignmentDecisions(...)`
    - `InvalidateWorkflowDataSnapshot()`
  - 移除舊路徑：多處直接賦值 `_latestPadMatchResult` / `_latestVisibleIndexAssignmentDecisionsByCadId`。
- 驗證：
  - 受執行中 `FreeformHelper.UI` 行程鎖檔影響，UI 專案 build/lint 待補跑。

### 2026-04-30 / S11.132 (Close)
- 範圍：
  - `BuildWorkflowDataSnapshot()` revision-aware cache。
  - Snapshot cache diagnostic counters。
- 變更重點：
  - cache hit 需同時滿足 valid flag 與 revision match，避免同一輪操作重複建立 frozen dictionaries。
  - `InvalidateWorkflowDataSnapshot()` 推進 revision，讓 match / layer / visibility / project-load 等來源變動後下一次 snapshot 可追蹤。
  - 新增 internal diagnostics：`WorkflowDataSnapshotRevision`、`WorkflowDataSnapshotBuildCount`。
- 驗證：
  - `FreeformHelperViewModelTests.WorkflowSnapshot.BuildWorkflowDataSnapshot_ReusesCacheUntilInvalidated`
  - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "SimulationSafetyAuditServiceTests|SimulationWorkspaceViewModelTests|FreeformHelperViewModelTests"`

### 2026-04-19 / S11.133
- 範圍：
  - 新增 `NotchDiffIdentityPipeline`（Application 層 diff identity 契約）。
  - `NotchCurrentVisibleDiffProjectionService` / `NotchApplySimulationService` 導入共用契約。
- 變更重點：
  - 收斂 raw/visible/projected 規則：
    - `BuildProjectionContract(...)`：`raw diff -> visible diff` 映射與 conflicted raw diff key 移除策略。
    - `ResolveAnchorDiff(...)` / `ResolveTargetDiff(...)`：anchor/target diff 同一投影入口。
  - 收斂 simulation active diff baseline 規則：
    - `BuildActiveDiffBaseline(...)`：diff key primary/suppressed 決策與 duplicate resolution 模型化。
    - `NotchApplySimulationService` 改由 pipeline baseline 生成 before/after diff key，維持既有 diagnostics 文案。
  - `NotchCurrentVisibleDiffProjectionResult` 新增 `ConflictedRawDiffKeyCount`，供上游 metrics/trace 使用。
- 驗證：
  - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~NotchCurrentVisibleDiffProjectionServiceTests|FullyQualifiedName~NotchApplySimulationServiceTests|FullyQualifiedName~NotchDiffIdentityPipelineTests"`
  - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj /p:UseAppHost=false`
  - `./scripts/tests/lint.ps1 -UseNoAppHost`

### 2026-04-19 / S11.134
- 範圍：
  - `NotchTableGenerator.Generation` orchestration 切分（不改數學輸出）。
  - v2.2 canonical -> v2.1 compatibility projection 呼叫介面收斂。
- 變更重點：
  - `GenerateCadAllocationCompatible` 改為 stage orchestration：
    - `BuildCadAllocationGenerationContext(...)`
    - `AppendLegacyCompatibleRows(...)`
    - `BuildCanonicalRows(...)`
    - `AppendCanonicalExports(...)`
  - 新增 `CadAllocationGenerationContext`，把 stage 間共享資料（profiles/enabled versions/export flags/canonical candidates/settings）顯式化。
  - `ProjectV22RowsToV21Rows(...)` 參數改為 `IReadOnlyList<NotchTableRow>`，消除不必要複製。
- 驗證：
  - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~NotchTableGeneratorTests|FullyQualifiedName~NotchApplySimulationServiceTests|FullyQualifiedName~NotchCurrentVisibleDiffProjectionServiceTests"`
  - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj /p:UseAppHost=false`
  - `./scripts/tests/lint.ps1 -UseNoAppHost`

### 2026-04-19 / S11.135
- 範圍：
  - Simulation duplicate diff 行為收斂為結構化契約。
  - workspace/runtime query 對 duplicate 策略與樣本的可觀測輸出。
- 變更重點：
  - `NotchApplySimulationModels` 新增：
    - `NotchApplySimulationDiffIdentityContract`
    - `NotchApplySimulationDuplicateDiffResolution`
    - `NotchApplySimulationDuplicateDiffResolutionStrategy`
  - `NotchApplySimulationResult` 加入 `DiffIdentityContract`。
  - `NotchApplySimulationService` 將 active diff baseline duplicate 決策轉為 `DiffIdentityContract` 輸出，並維持既有 diagnostics 字串相容。
  - `SimulationWorkspaceViewModel` 新增 duplicate diff 契約摘要（count/strategy/summary/sample）。
  - `RuntimeQueryUseCase.Commands.Simulation` 新增 `workspace.diffIdentity` payload。
- 驗證：
  - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~NotchApplySimulation"`
  - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~QuerySimulation_ReturnsWorkspaceAndRegularSnapshot"`
  - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj /p:UseAppHost=false`
  - `./scripts/tests/lint.ps1 -UseNoAppHost`

### 2026-04-19 / S11.136
- 範圍：
  - `docs/core/notch-overall-flow-mermaid.md`（重建並對齊現況）。
  - `docs/core/notch-v21-v22-flow.md`（流程/語意同步）。
  - `TODO.md`（slice 完成狀態同步）。
- 變更重點：
  - `notch-overall-flow-mermaid` 同步 `S11.134` stage orchestration：
    - `BuildCadAllocationGenerationContext -> BuildCanonicalCandidatesByDiff -> BuildCanonicalRows -> AppendCanonicalExports`。
  - 補上 projection 真實契約：
    - anchor diff 以 `CadPadId` 直接投影。
    - target diff 透過 `(IcIndex, RawTargetDiff)` 映射，僅唯一映射 key 會改寫，conflict key 保留 raw。
  - 補上 `S11.135` Simulation `diffIdentity` 契約輸出鏈路（含 `merge-sum-active-regular-pads` 策略）。
  - `notch-v21-v22-flow` 同步修正 target diff 語意、CadAllocation 詳細流程圖、Simulation 共用契約段落。
- 驗證：
  - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj /p:UseAppHost=false`
  - `./scripts/tests/lint.ps1 -UseNoAppHost`

### 2026-04-19 / S11.137
- 範圍：
  - `NotchDiffIdentityPipeline` active diff baseline duplicate 聚合。
  - `NotchApplySimulationService` duplicate diagnostics/contract strategy。
  - `SimulationWorkspaceViewModel` duplicate strategy 顯示與 sample 文案。
- 變更重點：
  - duplicate diff key baseline 改為 `merge-sum-active-regular-pads`：
    - 同 `(IC,Diff)` 的 active regular pad 值改為加總，不再 keep-first suppress。
  - duplicate diagnostics 文案改為 merged contributors（`merged REG ... by sum`）。
  - runtime/UI strategy string 改為 `merge-sum-active-regular-pads`。
  - 補測試更新：
    - `NotchDiffIdentityPipelineTests` duplicate baseline 預期改為加總值。
    - `NotchApplySimulationServiceTests` duplicate case after value/strategy 期望同步改為 merge-sum。
    - `RuntimeQueryUseCaseTests` simulation `diffIdentity` strategy text 同步更新。
- 驗證：
  - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~NotchDiffIdentityPipelineTests|FullyQualifiedName~NotchApplySimulationServiceTests|FullyQualifiedName~QuerySimulation_ReturnsWorkspaceAndRegularSnapshot"`
  - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj /p:UseAppHost=false`
  - `./scripts/tests/lint.ps1 -UseNoAppHost`

### 2026-04-19 / S11.138
- 範圍：
  - settings 五個分頁視圖的主要布林選項呈現統一。
- 變更重點：
  - `CheckBox` 主要區塊改為一致 `ToggleSwitch` row pattern（label + step note + switch）。
  - 保留原綁定與設定語意，不調整 workflow/演算法路徑。
- 驗證：
  - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj /p:UseAppHost=false`
  - `./scripts/tests/lint.ps1 -UseNoAppHost`

