# Beta 0.5 Notch Refactor Slices (Start Anchor)

Last updated: 2026-04-19\
Applicable branch: `beta0.5`

## Refactor Start Marker
- Marker：`BETA05-NOTCH-REFACTOR-START-2026-04-19`
- Description: Subsequent Beta 0.5 notch flow refactor commits use this marker as the starting point for tracking.

## Commit Record Requirements (Required for Subsequent Slices)
Each refactor commit (from S11.132 onward) must include the following fields in its commit body:

```text
Refactor-Start: BETA05-NOTCH-REFACTOR-START-2026-04-19
Refactor-Slice: S11.xxx
Refactor-Record: docs/core/notch-beta05-refactor-slices.md
Change-Items: item1; item2; item3
```

Recommended commit subject format:

```text
refactor(beta0.5-S11.xxx): <slice summary>
```

## Slice List

### S11.132 Snapshot Consolidation
- Scope:
  - `WorkflowDataSnapshot` construction and caching.
  - Consolidation of snapshot invalidation sources in `FreeformHelperViewModel`.
- Goals:
  - Build the snapshot only once per operation batch.
  - Reduce repeated `ToFrozenDictionary` costs.

### S11.133 Diff Identity Pipeline
- Scope:
  - Intermediate contracts for Raw diff / Visible diff / Projected row identity.
  - Interfaces between `NotchCurrentVisibleDiffProjectionService` and the upstream flow.
- Goals:
  - A single source of diff identity semantics to avoid repeated derivation in multiple places.
  - Preserve existing conflict fallback behavior while making it traceable and testable.

### S11.134 Notch Generation Orchestration
- Scope:
  - `NotchTableGenerator.Generation*`。
  - Stage separation (candidate -> select -> serialize -> compatibility projection).
- Goals:
  - Change only flow responsibility boundaries, without changing mathematical output.
  - Improve testability and explainability.

### S11.135 Simulation Contract Consolidation
- Scope:
  - Handling of duplicate diff keys in `NotchApplySimulationService`.
  - Alignment with the notch row identity contract.
- Goals:
  - Move Simulation from "corrective rules" to a "contract consumer".
  - Make duplicate diff sources and the selected strategy clearly traceable in diagnostics.

### S11.136 Documentation Consistency
- Scope:
  - `docs/core/notch-overall-flow-mermaid.md`
  - `docs/core/notch-v21-v22-flow.md` and related specs
- Goals:
  - Align documentation with code behavior.
  - Clearly distinguish the canonical path from the compatibility path.

### S11.137 Simulation Duplicate Diff Aggregation Fix
- Scope:
  - `NotchDiffIdentityPipeline.BuildActiveDiffBaseline(...)`
  - Duplicate diff contract display in `NotchApplySimulationService` / `SimulationWorkspaceViewModel`
- Goals:
  - Change the duplicate diff baseline from keep-first to merge-sum to avoid losing sensing values on the active surface.
  - Keep Step4 visible diff assignment logic unchanged; fix only the Simulation aggregation layer.

### S11.138 Settings Toggle Layout Consistency
- Scope:
  - `SettingsGeneralSectionView.axaml`
  - `SettingsStep2SectionView.axaml`
  - `SettingsStep3SectionView.axaml`
  - `SettingsStep4SectionView.axaml`
  - `SettingsStep5SectionView.axaml`
- Goals:
  - Standardize the main Boolean options as scannable switch rows to reduce the visual load of multiple checkboxes on settings pages.
  - Make only UI presentation consistent, without changing settings logic or binding semantics.

## Validation Baseline
- On completion of each slice, run at least:
  - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
  - Tests for the corresponding scope (the minimum necessary)
  - `./scripts/tests/lint.ps1 -UseNoAppHost`
- If a running UI process locks files, explicitly record the blocker and the plan for completing validation in the commit record.

## Execution Log

### 2026-04-19 / S11.132 (Phase A)
- Scope:
  - Introduce snapshot caching and invalidation in `FreeformHelperViewModel.Core`.
  - Add invalidation points to the Step1/Step4/SeeRegular/LoadProject source data update paths.
- Key changes:
  - Change `BuildWorkflowDataSnapshot()` to cache-first.
  - Add unified entry points:
    - `SetLatestPadMatchResult(...)`
    - `SetLatestVisibleIndexAssignmentDecisions(...)`
    - `InvalidateWorkflowDataSnapshot()`
  - Remove the old paths: direct assignments to `_latestPadMatchResult` / `_latestVisibleIndexAssignmentDecisionsByCadId` in multiple places.
- Validation:
  - UI project build/lint still need to run because a running `FreeformHelper.UI` process locked files.

### 2026-04-30 / S11.132 (Close)
- Scope:
  - `BuildWorkflowDataSnapshot()` revision-aware cache。
  - Snapshot cache diagnostic counters。
- Key changes:
  - A cache hit requires both a valid flag and a revision match to avoid rebuilding frozen dictionaries within the same operation cycle.
  - `InvalidateWorkflowDataSnapshot()` advances the revision, making the next snapshot traceable after changes to sources such as match / layer / visibility / project-load.
  - Add internal diagnostics: `WorkflowDataSnapshotRevision`, `WorkflowDataSnapshotBuildCount`.
- Validation:
  - `FreeformHelperViewModelTests.WorkflowSnapshot.BuildWorkflowDataSnapshot_ReusesCacheUntilInvalidated`
  - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "SimulationSafetyAuditServiceTests|SimulationWorkspaceViewModelTests|FreeformHelperViewModelTests"`

### 2026-04-19 / S11.133
- Scope:
  - Add `NotchDiffIdentityPipeline` (the Application-layer diff identity contract).
  - Introduce the shared contract in `NotchCurrentVisibleDiffProjectionService` / `NotchApplySimulationService`.
- Key changes:
  - Consolidate raw/visible/projected rules:
    - `BuildProjectionContract(...)`: `raw diff -> visible diff` mapping and the removal strategy for conflicted raw diff keys.
    - `ResolveAnchorDiff(...)` / `ResolveTargetDiff(...)`: a single projection entry point for anchor/target diff.
  - Consolidate simulation active diff baseline rules:
    - `BuildActiveDiffBaseline(...)`: model diff key primary/suppressed decisions and duplicate resolution.
    - `NotchApplySimulationService` uses the pipeline baseline to generate before/after diff keys, preserving existing diagnostics text.
  - Add `ConflictedRawDiffKeyCount` to `NotchCurrentVisibleDiffProjectionResult` for upstream metrics/trace.
- Validation:
  - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~NotchCurrentVisibleDiffProjectionServiceTests|FullyQualifiedName~NotchApplySimulationServiceTests|FullyQualifiedName~NotchDiffIdentityPipelineTests"`
  - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj /p:UseAppHost=false`
  - `./scripts/tests/lint.ps1 -UseNoAppHost`

### 2026-04-19 / S11.134
- Scope:
  - Separate `NotchTableGenerator.Generation` orchestration (without changing mathematical output).
  - Consolidate the call interface for v2.2 canonical -> v2.1 compatibility projection.
- Key changes:
  - Change `GenerateCadAllocationCompatible` to stage orchestration:
    - `BuildCadAllocationGenerationContext(...)`
    - `AppendLegacyCompatibleRows(...)`
    - `BuildCanonicalRows(...)`
    - `AppendCanonicalExports(...)`
  - Add `CadAllocationGenerationContext` to make data shared between stages (profiles/enabled versions/export flags/canonical candidates/settings) explicit.
  - Change the `ProjectV22RowsToV21Rows(...)` parameter to `IReadOnlyList<NotchTableRow>` to eliminate unnecessary copying.
- Validation:
  - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~NotchTableGeneratorTests|FullyQualifiedName~NotchApplySimulationServiceTests|FullyQualifiedName~NotchCurrentVisibleDiffProjectionServiceTests"`
  - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj /p:UseAppHost=false`
  - `./scripts/tests/lint.ps1 -UseNoAppHost`

### 2026-04-19 / S11.135
- Scope:
  - Consolidate Simulation duplicate diff behavior into a structured contract.
  - Observable workspace/runtime query output for the duplicate strategy and samples.
- Key changes:
  - Add to `NotchApplySimulationModels`:
    - `NotchApplySimulationDiffIdentityContract`
    - `NotchApplySimulationDuplicateDiffResolution`
    - `NotchApplySimulationDuplicateDiffResolutionStrategy`
  - Add `DiffIdentityContract` to `NotchApplySimulationResult`.
  - `NotchApplySimulationService` converts active diff baseline duplicate decisions into `DiffIdentityContract` output while preserving compatibility with existing diagnostics strings.
  - Add a duplicate diff contract summary (count/strategy/summary/sample) to `SimulationWorkspaceViewModel`.
  - Add the `workspace.diffIdentity` payload to `RuntimeQueryUseCase.Commands.Simulation`.
- Validation:
  - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~NotchApplySimulation"`
  - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~QuerySimulation_ReturnsWorkspaceAndRegularSnapshot"`
  - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj /p:UseAppHost=false`
  - `./scripts/tests/lint.ps1 -UseNoAppHost`

### 2026-04-19 / S11.136
- Scope:
  - `docs/core/notch-overall-flow-mermaid.md` (rebuild and align with the current state).
  - `docs/core/notch-v21-v22-flow.md` (synchronize flow/semantics).
  - `TODO.md` (synchronize slice completion status).
- Key changes:
  - Synchronize `notch-overall-flow-mermaid` with `S11.134` stage orchestration:
    - `BuildCadAllocationGenerationContext -> BuildCanonicalCandidatesByDiff -> BuildCanonicalRows -> AppendCanonicalExports`。
  - Add the actual projection contract:
    - Project anchor diff directly by `CadPadId`.
    - Map target diff through `(IcIndex, RawTargetDiff)`; rewrite only uniquely mapped keys and retain raw values for conflict keys.
  - Add the `S11.135` Simulation `diffIdentity` contract output chain (including the `merge-sum-active-regular-pads` strategy).
  - Synchronize the corrections to target diff semantics, the detailed CadAllocation flowchart, and the shared Simulation contract section in `notch-v21-v22-flow`.
- Validation:
  - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj /p:UseAppHost=false`
  - `./scripts/tests/lint.ps1 -UseNoAppHost`

### 2026-04-19 / S11.137
- Scope:
  - Duplicate aggregation for the `NotchDiffIdentityPipeline` active diff baseline.
  - `NotchApplySimulationService` duplicate diagnostics/contract strategy。
  - Duplicate strategy display and sample text in `SimulationWorkspaceViewModel`.
- Key changes:
  - Change the duplicate diff key baseline to `merge-sum-active-regular-pads`:
    - Sum the values of active regular pads with the same `(IC,Diff)` instead of keep-first suppression.
  - Change duplicate diagnostics text to merged contributors (`merged REG ... by sum`).
  - Change the runtime/UI strategy string to `merge-sum-active-regular-pads`.
  - Add test updates:
    - Change the `NotchDiffIdentityPipelineTests` duplicate baseline expectation to the summed value.
    - Align the `NotchApplySimulationServiceTests` duplicate case after value/strategy expectations with merge-sum.
    - Update the `RuntimeQueryUseCaseTests` simulation `diffIdentity` strategy text accordingly.
- Validation:
  - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~NotchDiffIdentityPipelineTests|FullyQualifiedName~NotchApplySimulationServiceTests|FullyQualifiedName~QuerySimulation_ReturnsWorkspaceAndRegularSnapshot"`
  - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj /p:UseAppHost=false`
  - `./scripts/tests/lint.ps1 -UseNoAppHost`

### 2026-04-19 / S11.138
- Scope:
  - Standardize the main Boolean option presentation across the five settings tab views.
- Key changes:
  - Change the main `CheckBox` sections to a consistent `ToggleSwitch` row pattern (label + step note + switch).
  - Preserve existing bindings and settings semantics without changing workflow/algorithm paths.
- Validation:
  - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj /p:UseAppHost=false`
  - `./scripts/tests/lint.ps1 -UseNoAppHost`

