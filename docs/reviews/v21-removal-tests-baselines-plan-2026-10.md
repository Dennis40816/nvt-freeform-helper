# V21/Legacy Complete Removal: Tests and Baselines Plan

- Inventory date: 2026-10-03; source revision: `7477b07d3969ad6ad15960d0d1ac8b60ef37a9bc`.
- Related work item: [“V21/Legacy Complete Removal” in TODO.md](../../TODO.md). This document is planning only; it does not change checkboxes, the roadmap, code, or tests.
- The owner's 「先保留」(keep it for now) decision remains in effect: V21 and `LegacyRegularAnchor` remain unchanged, protected by the existing zero-diff gates; no new legacy convergence/equivalence work is added. No removal version, schedule, or migration policy for old projects is proposed.
- References: sections 0, 2, and 3 of the [1.3.x roadmap](../guides/refactor-roadmap-1.3.x.md), [Notch contract](../reference/notch-system-reference.md), [Runtime CLI contract](../reference/runtime-cli-plan.md), and [3635 regression baseline](../performance/regression-baseline-3635.md).
- No `example/` content was read, copied, or quoted. The private data names below come from strings in tests/scripts and public repository documents; the current contents, existence, or hashes of the private files have not been verified. This does not authorize removal or golden updates.

## 1. Counting Rules and CI Assignment

The counting unit is a test **method**, not a case expanded by xUnit. Partial classes are counted together; a theory that runs both versions still counts as one method, and deleting V21 InlineData does not mean deleting the entire method. “Relevant/class total” in the tables lists only the methods relevant to this inventory; unlisted methods remain.

The inventory includes explicit V21/Legacy contracts, data or settings for both versions, and methods that actually obtain data for both versions through shared fixtures/helpers. Tests that only create a default ViewModel without executing or verifying a version contract do not count as dual-version tests. Three boundaries that must be protected against accidental deletion are also included: shared Q7 allocation, the legacy table cache, and an E2E benchmark whose version is determined by a private project. Each is explicitly identified and must not be treated as a V21-only test.

Disposition symbols: **Delete** = dedicated assertions/methods may be deleted after removal is approved and the corresponding behavior is gone; **Convert** = establish V22 characterization first, then remove the V21/Legacy portion while preserving the shared contract; **Keep** = still used by V22/shared behavior. All dispositions are future conditions; everything remains unchanged now.

[run-tests.ps1](../../scripts/tests/run-tests.ps1) defines groups; [verify.ps1](../../scripts/verify.ps1) maps groups to shards in [.github/workflows/ci.yml](../../.github/workflows/ci.yml):

| CI shard | run-tests.ps1 groups | Notes |
|---|---|---|
| `core` | `notch-core`, `application`, `infrastructure`, `uncategorized` | The same class can appear in multiple groups; do not count methods twice. `notch-golden` is a subset of `notch-core`, not a separate CI shard. |
| `ui` | `ui-stable` | Local `ui-core` is the union of `ui-stable` and `ui-viewmodel`. |
| `viewmodel` | `ui-viewmodel` | Separate, nonblocking shard for `FreeformHelperViewModelTests`. |
| `snapshots` | `ui-snapshots` | UI source hash, rendered snapshot, layout, and persistence contract. |

Currently, `run-refactor-gate.ps1` always runs `notch-core`, `application`, `infrastructure`, `ui-core`, `ui-snapshots`, and `uncategorized`; the old `Include*` switches are no longer required to enable these groups. Historical case counts in the roadmap cannot be used directly as test-count thresholds after removal.

### 1.1 Counts by Class

All sources are under `tests/FreeformHelper.Tests/`; the next section lists each relevant method. The total is **23 classes and 100 relevant methods/methods requiring confirmation**, including the three shared/conditional boundaries above; this is not the number that can be deleted.

| Class | Relevant/class total methods | run-tests.ps1 group | CI shard |
|---|---:|---|---|
| `NotchTableGeneratorTests` | 22/42 | `notch-core`, `application` | `core` |
| `NotchTableExporterTests` | 10/27 | `notch-core`, `application` | `core` |
| `NotchSettingsTests` | 2/8 | `notch-core` | `core` |
| `NotchV22CompensationServiceTests` | 1/26 | `notch-core`, `application` | `core` |
| `NotchCadOutputFwDiffProjectionServiceTests` | 1/5 | `notch-core` | `core` |
| `NotchApplySimulationServiceTests` | 6/11 | `notch-core` | `core` |
| `NotchApplySimulationReviewUseCaseTests` | 1/2 | `notch-core` | `core` |
| `SimulationWorkspaceUseCaseTests` | 1/3 | `uncategorized` | `core` |
| `NotchExampleCExportDriftTests` | 2/3 | `notch-golden`, `notch-core` | `core` |
| `NotchGoldenBaselineTests` | 1/1 | `notch-golden`, `notch-core` | `core` |
| `Tm81NotchAcceptanceMatrixTests` | 1/1 | `notch-golden`, `notch-core` | `core` |
| `Notch3635GenerationBenchmarkTests` | 1/1 | `uncategorized` | `core` (opt-in workload) |
| `Cad3635EndToEndBenchmarkTests` | 1/1 | `uncategorized` | `core` (opt-in workload; version requires confirmation) |
| `FreeformHelperViewModelTests` | 9/158 | `ui-viewmodel`, local `ui-core` | `viewmodel` |
| `NotchExportSelectionViewModelTests` | 26/48 | `ui-stable`, local `ui-core` | `ui` |
| `NotchExportSelectionProjectionBuilderTests` | 2/2 | `uncategorized` | `core` |
| `NotchExportSelectionRowSelectionServiceTests` | 5/5 | `uncategorized` | `core` |
| `NotchExportSelectionSummaryProjectorTests` | 2/2 | `uncategorized` | `core` |
| `RuntimeQueryUseCaseTests` | 2/18 | `notch-core` | `core` |
| `ProjectFileMigrationTests` | 1/9 | `application` | `core` |
| `ProjectStoreTests` | 1/2 | `application` | `core` |
| `ProjectModelCollectionIsolationTests` | 1/2 | `uncategorized` | `core` |
| `NotchExportGenerationCacheServiceTests` | 1/12 | `notch-core` | `core` |

No entire test class in this list can be deleted now. Golden/benchmark classes with only one method also protect V22 and must be kept or rewritten.

## 2. Method-Level Inventory

### 2.1 Generator, Exporter, and Settings

Source: [NotchTableGeneratorTests.cs](../../tests/FreeformHelper.Tests/Application/Notch/NotchTableGeneratorTests.cs), 22 methods.

| Method | Current scope | Disposition after removal/prerequisites |
|---|---|---|
| `Generate_Skips_When_Freeform_None` | Legacy, V22 | Delete; first confirm the CadAllocation eligibility/no-op contract; do not directly reuse the Legacy empty-table expectation. |
| `Generate_LegacyRegularAnchor_V22_BuildsCompatibilityGeometryRow` | Legacy, V22 9-column geometry row | Delete; do not treat this compatibility payload as a canonical V22 expectation. |
| `Generate_LegacyRegularAnchor_VersionDispatchMatchesEligibility` | Legacy, both versions; 3 freeform InlineData entries | Delete; canonical V22 XWay/YWay/XYWay eligibility coverage must exist first. |
| `Generate_LegacyRegularAnchor_FreezesRequestBeforeInitialProgressCallback` | Legacy, V22; callback changes computation mode | Delete; preserve normal request freezing coverage first. |
| `Generate_LegacyRegularAnchor_OwnsEnabledVersionsBeforeInitialProgressCallback` | Legacy, V22→V21 | Delete; a single version no longer has cross-version mutation. |
| `Generate_LegacyRegularAnchor_FreezesRowSettingsForUnbuiltRowsAndNextCallObservesChanges` | Legacy, V22; threshold/LenScale/NullValue | Delete; first lock the current request and next request for V22 threshold/sentinel. |
| `Generate_LegacyRegularAnchor_FreezesLinkedThresholdForUnbuiltRows` | Legacy, linked Q7 threshold for both versions | Delete; native V22 threshold admission and request capture must exist first. |
| `Generate_V21_Adds_Row_When_Cad_Matched` | V21, CadAllocation | Convert; first lock the generated result for matched CAD using V22. |
| `Generate_V21_YWay_PreservesLegacyNeighborOrientation` | Legacy, V21 | Delete; canonical V22 YWay source/target orientation must exist first; do not copy expectations from Legacy. |
| `Generate_V21_EncodesNeighborLegMagnitudeAsQ7` | Legacy, V21 payload Q7 | Delete; preserve the independent V22 signed percent contract. |
| `Generate_Skips_When_Cad_Not_Matched` | Legacy, V22 | Delete; first confirm the opposite contract that CadAllocation can generate using a geometry anchor. |
| `ProjectCadAllocationResolvedBatch_ReusesVersionNeutralCandidatesAcrossFinalRequests` | V21-only/V22-only/both | Convert; preserve fresh parity for the same batch across different V22 threshold/NullValue requests. |
| `Generate_CadAllocationMode_FreezesResolutionInputsAtCandidatePhaseStart` | Both versions; 2 callback timing cases | Convert; keep both timing cases and remove only V21 expected rows. |
| `Generate_CadAllocationMode_FreezesProjectionRequestAtCandidatePhaseStart` | V22→V21 mutation; 2 timing cases | Convert; lock V22 sentinel/threshold mutation instead; do not simply keep the one existing V22 InlineData entry. |
| `Generate_CadAllocationMode_ReportsOnlyExecutablePhases` | Both versions; 2 cases with/without CAD profile | Convert; preserve the four phases, empty profile, and progress order. |
| `Generate_V21_WorksFromOverlapMatchAndAutoDetectPipeline` | Legacy, V21 | Convert; establish V22 normal matching→freeform→generator characterization before deleting the old method. |
| `Generate_CadAllocationMode_ProducesFewerRowsThanLegacy_WhenCadSpansMultipleRegularPads` | Both versions, both computation modes | Convert; use the unique anchor/row set for the same CAD in V22 as the fixed expectation, and remove the 4 versus 2 count relative to Legacy. |
| `Generate_CadAllocationMode_UsesGeometryAnchor_WhenLegacyMatchMissing` | V21, CadAllocation | Convert; switch to V22 while still verifying geometry fallback when no matched CAD exists. |
| `Generate_LegacyRegularAnchor_V22_PreservesCompatibilityNeighborOrientation` | Legacy, V22 9-column | Delete; canonical V22 orientation characterization must exist first. |
| `ProjectCadAllocationResolvedBatch_ValidatesCombinedPercentOnlyAfterFinalThresholdAdmission` | V21/V22/both | Convert; keep V22 threshold rejection/admission and overflow timing. |
| `Generate_CadAllocationMode_WhenSubMicroPolygonChangeFlipsAllocation_UsesCurrentGeometry` | Inline expected snapshots for both versions | Convert; preserve V22 left/right anchors, small geometry changes, and cache identity. |
| `Generate_CadAllocationMode_IsDeterministicAcrossRuns` | Both versions, across ICs, repeated generation | Convert; verify five reruns against fixed V22 row order/payload. |

Source: [NotchTableExporterTests.cs](../../tests/FreeformHelper.Tests/Application/Notch/NotchTableExporterTests.cs), 10 methods.

| Method | Current scope | Disposition after removal/prerequisites |
|---|---|---|
| `ExportAsCsv_UsesFixedPayloadColumnsForMixedValueLengthRows` | V21 9-column + V22 7-column | Convert; keep V22 CSV column order, escaping, and blank padding; whether to reduce columns requires a separate product contract. |
| `ExportAndSimulation_RejectNullDiffOutsideFirmwareUint16Domain` | V21/V22 InlineData | Convert; delete only the V21 case and keep the same exception for the V22 exporter/simulation. |
| `ExportAndSimulation_PreserveUint16MaxDiffWhenSentinelIsAdjacent` | V21 Legacy/V22 CadAllocation InlineData | Convert; keep the exact V22 results for the valid `65535` diff and adjacent sentinel. |
| `ExportAsCInitializer_SnapshotsNullDiffBeforeReadingCallerCollections` | V21 Legacy/V22 CadAllocation InlineData | Convert; keep V22 sentinel capture; do not delete the caller-collection mutation test. |
| `ExportAsCInitializer_EmitsVersionSpecificFwFiles` | Both versions, exported separately; mixed-version C fails fast | Convert; keep V22 ABI/formatting; remove mixed-version rejection assertions along with V21 rows that can no longer be constructed. |
| `ExportAsCInitializer_GccCompilesFwStyleExports_WhenGccIsAvailable` | Compiles C for both versions in the same method | Convert; preserve V22 GCC compilation. |
| `ExportAsCInitializer_GccRuntimeMatchesCSharpSimulationForV21_WhenGccIsAvailable` | V21 CadAllocation | Delete; V22 GCC/C# parity must remain executable first. |
| `ExportAsCInitializer_GccRuntimeMatchesCSharpSimulationForV21LegacyRegularAnchor_WhenGccIsAvailable` | V21 Legacy | Delete; only after the Legacy cells-only/EMS contract is gone. |
| `ExportAsCInitializer_GccAndCSharpSimulationMatchV21Q7BoundaryContract_WhenGccIsAvailable` | V21 magnitude 0/1/127/128/129/255 × ADD/SUB | Delete; do not remove shared allocation rounding; lock V22 signed percent boundaries separately. |
| `ExportAsCInitializer_V21ThreeTerms_SplitsNodesAndPreservesInt16Wrap` | V21 continuation/INT16 narrowing | Delete; first complete V22 multiple-term writeback/wrap characterization; the general continuation case alone is insufficient. |

Other relevant methods:

| Class/source | Method | Disposition |
|---|---|---|
| [NotchSettingsTests](../../tests/FreeformHelper.Tests/Application/Settings/NotchSettingsTests.cs) (2 methods) | `ProjectSettingsValidate_RejectsV21ThresholdOutsideIndependentQ7Range`; `ProjectSettingsValidate_AcceptsV21ThresholdQ7Boundaries` | Delete; each has 2 InlineData entries. Characterization of the valid range/boundaries for the native V22 threshold must exist first; preserve NullValue and compensation model tests in the same class. |
| [NotchV22CompensationServiceTests](../../tests/FreeformHelper.Tests/Application/Notch/NotchV22CompensationServiceTests.cs) (1 shared boundary) | `BuildAllocations_UsesAwayFromZeroAtV21ThresholdHalfStep` | Keep/rename: actually tests `NotchAllocationService.BuildAllocations` without selecting V21. Q7-positive allocation still serves V22; do not delete it because of its name. |
| [NotchCadOutputFwDiffProjectionServiceTests](../../tests/FreeformHelper.Tests/Application/Notch/NotchCadOutputFwDiffProjectionServiceTests.cs) (1 method) | `Project_WhenCadDiffsShiftLeaveGap_GapDoesNotRemainAsAnchor` | Convert; the fixture contains only V21; first cover the same gap/anchor mapping behavior with V22. The existing typed V22 anchor-only case does not automatically prove this gap scenario. |

### 2.2 Simulation and Review

Source: [NotchApplySimulationServiceTests.cs](../../tests/FreeformHelper.Tests/Workflow/NotchApplySimulationServiceTests.cs), 6 methods.

| Method | Current scope | Disposition |
|---|---|---|
| `Simulate_WhenCanonicalContinuationRowsAreProjected_PreservesEachFirmwareContract` | Runs both versions in the same method | Convert; preserve V22 continuation cells, actions, legs, and contract text. |
| `Simulate_WhenVersionIsV21_UsesProjectedCanonicalContract` | V21 imported 9-column row | Delete; preserve the V22 imported canonical row path. |
| `Simulate_WhenV21LegTargetsItsOwnSource_KeepsActionFlowAlignedWithFirmwareCells` | V21 self-target | Delete; V22 self-target firmware/cells/flow consistency must exist first. |
| `Simulate_WhenV21ContinuationRowsClampIndependently_ReportsEffectiveRetainedPercent` | V21 continuation Q7/clamp | Delete; first lock V22 continuation retained percent/INT16 writeback. |
| `Simulate_WhenFrameIsOutsideInt16Domain_UsesFirmwareBaselineForCellsAndAudit` | V21/V22 InlineData | Convert; keep quantization, nonfinite values, and the cells/audit baseline for the V22 case. |
| `Simulate_WhenV21ProjectionReferencesMissingDiffs_WritesExactDiagnostics` | V21 missing source/target | Delete; V22 missing-diff diagnostics and dropped-node/action consistency must exist first. |

| Class/source | Method (1 each) | Disposition |
|---|---|---|
| [NotchApplySimulationReviewUseCaseTests](../../tests/FreeformHelper.Tests/Workflow/NotchApplySimulationReviewUseCaseTests.cs) | `BuildSnapshot_WhenV21RowsImported_UsesSharedProjectedCanonicalPath` | Delete; preserve the review/snapshot projection of `BuildSnapshot_WhenV22RowsImported_UsesSharedSimulationPath`. |
| [SimulationWorkspaceUseCaseTests](../../tests/FreeformHelper.Tests/UI/Services/SimulationWorkspaceUseCaseTests.cs) | `BuildScenarioSnapshot_PropagatesLegacyCompatibilityModeToTheSharedFirmwareProjection` | Delete; keep the other two V22 scenario/copper replay tests; EMS/global-flow audit is not removed along with Legacy. |

### 2.3 Golden and Benchmark

| Class/source | Method | Disposition |
|---|---|---|
| [NotchExampleCExportDriftTests](../../tests/FreeformHelper.Tests/Application/Notch/NotchExampleCExportDriftTests.cs) (2 methods) | `Boe3635_SignedGoldenManifest_MatchesCheckedInArtifacts`; `Boe3635_CheckedInCExample_MatchesCurrentExportPipeline` | Convert; keep V22 and the input manifest lock in the former; delete only V21 InlineData in the latter. Keep `NormalizeText_OnlyNormalizesLineEndingsAndSingleEofNewline`. |
| [NotchGoldenBaselineTests](../../tests/FreeformHelper.Tests/Application/Notch/NotchGoldenBaselineTests.cs) (1 method) | `RealProjects_MatchGoldenBaseline` | Convert; both the method internals and workflow helper force both versions; preserve 3635/TM8.1 regression after converting to V22 characterization. |
| [Tm81NotchAcceptanceMatrixTests](../../tests/FreeformHelper.Tests/Application/Notch/Tm81NotchAcceptanceMatrixTests.cs) (1 method) | `TM81_MatchesAcceptanceMatrixSnapshot` | Convert; internally forces both versions. Preserve V22 no-op/warning, ToFull, diff repair, and case contracts. |
| [Notch3635GenerationBenchmarkTests](../../tests/FreeformHelper.Tests/Application/Notch/Notch3635GenerationBenchmarkTests.cs) (1 method) | `Boe3635_NotchGenerationBenchmark_WhenEnabled` | Convert; `NotchCurrentWorkflowTableTestHelper.GenerateRawTableAsync` forces both versions. Change to a V22 workload and remeasure; keep it opt-in. |
| [Cad3635EndToEndBenchmarkTests](../../tests/FreeformHelper.Tests/UI/Benchmarks/Cad3635EndToEndBenchmarkTests.cs) (1 conditional boundary) | `Boe3635_EndToEndWorkflowBenchmark_WhenEnabled` | Keep/convert after confirmation; Load project→Step5 follows the saved version/file type; the code does not force both versions. Public 3635 provenance records both versions enabled, but this inventory did not read the private project; do not claim that this benchmark always generates C for both versions. |

`NotchToFullCoverageSnapshotBuilder`/`NotchCurrentWorkflowTableTestHelper` are helpers, not test classes; the latter sets `EnableV21=true` and `EnableV22=true` at both the raw/export entry points, so changing only the theory InlineData in the table above would miss the helper.

### 2.4 Main ViewModel, RuntimeQuery, and Project

`FreeformHelperViewModelTests` (9 methods; sources are the following partial files, all under `UI/ViewModels/`):

| Partial file | Method | Disposition |
|---|---|---|
| `FreeformHelperViewModelTests.Basics.CoreFlags.cs` | `NotchVersion_DefaultsToV21AndV22Enabled` | Convert; V22-only default characterization must exist first. |
| Same as above | `LenScale_RemainsHidden_WhenV21Enabled` | Delete; first lock the visibility of the remaining V22 settings after V21/Legacy settings are removed. |
| `FreeformHelperViewModelTests.NotchExportNaming.cs` | `ResolveSuggestedNotchExportBaseName_WhenMixedVersionsOrNonC_KeepsRequestedName` | Convert; remove the mixed-version scenario and keep the requested name for CSV/non-C. |
| `FreeformHelperViewModelTests.NotchExportCache.cs` | `ExportNotchCommand_LegacyRegularAnchor_FinalSettingChangesRemainRequestSpecificTableMisses` | Delete; only after the legacy request-specific table cache is gone. |
| Same as above | `ExportNotchCommand_WhenVersionChanges_ReusesResolvedBatchAndMatchesFreshProjection` | Convert; replace the 2 V21↔V22 theory cases with warm/fresh parity for V22 final-only requests; do not delete the cache contract along with them. |
| Same as above | `ExportNotchCommand_ThresholdReprojectsButResolutionSettingRebuildsBatch` | Convert; currently exports only V21. First lock that V22 threshold/NullValue can reproject, computation settings require a rebuild, and a zero-row batch can be reused. |
| `FreeformHelperViewModelTests.NotchResolvedSnapshot.cs` | `ResolvedSnapshot_OutputOnlyStateReusesIdentity_ComputationChangeInvalidatesIt` | Convert; remove V21-only/both toggles; keep output-only identity for V22 profile/format/threshold and computation invalidation. |
| `FreeformHelperViewModelTests.SettingsPersistence.ProjectReplay.cs` | `LoadThenSaveProject_PreservesLegacyNotchComputationAndVersionFields` | Deletion/replacement must wait for the decision on handling old projects; currently Legacy + V22, so it cannot be deleted just because the V21 exporter is deleted. |
| Same as above | `SaveThenLoadProject_PersistsStep5NotchVersionSelection` | Convert; originally tests V21-only save/reload. V22 settings/selection roundtrip must exist first; this plan does not decide how to migrate old fields. |

| Class/source | Method | Disposition |
|---|---|---|
| [RuntimeQueryUseCaseTests.ExportAndStatus.cs](../../tests/FreeformHelper.Tests/UI/Services/RuntimeQueryUseCaseTests.ExportAndStatus.cs) (2 methods) | `ExecuteAsync_QueryStatus_IncludesCadLoadSpinnerDebugTelemetry`; `ExecuteAsync_QueryExportNotch_AcceptsVersionedCFormat_WhenWorkflowNotReady` | Convert; in the former, remove the `enableV21` schema assertion and keep V22/export state and spinner telemetry; the latter currently uses V21=true/V22=false; keep state restoration after temporarily switching to the V22 format and the workflow-not-ready failure. |
| [ProjectFileMigrationTests.cs](../../tests/FreeformHelper.Tests/Application/Project/ProjectFileMigrationTests.cs) (1 method) | `Load_MigratesNullCollections` | Convert; remove the assertion that the default collection contains V21; preserve null collection normalization and V22 fallback. |
| [ProjectStoreTests.cs](../../tests/FreeformHelper.Tests/Application/Project/ProjectStoreTests.cs) (1 method) | `SaveAndLoad_RoundTripsMatchingAndIndexMappingSettings` | Convert; the `UiNotchSnapshot.EnabledVersions` fixture contains both versions; preserve the remaining matching/mapping/mask/V22 settings roundtrip. |
| [ProjectModelCollectionIsolationTests.cs](../../tests/FreeformHelper.Tests/Application/Project/ProjectModelCollectionIsolationTests.cs) (1 method) | `ProjectUiSnapshot_ListSetters_CloneInputCollections` | Convert; use a V22 list to preserve defensive copying; do not delete the isolation contract because of a fixture with strings for both versions. |
| [NotchExportGenerationCacheServiceTests.cs](../../tests/FreeformHelper.Tests/UI/Services/NotchExportGenerationCacheServiceTests.cs) (1 shared boundary) | `CadAllocationBatchAndLegacyTable_AreTypeIsolatedAndTrackLatestProjectedRowCount` | Convert; does not generate a LegacyRegularAnchor row; actually verifies isolation between the normal batch and request-specific table cache. Preserve normal batch lease/row-count coverage before removing the legacy cache half. Keep the remaining fault/retry/epoch/collision methods. |

### 2.5 Mixed Fixtures for Export Selection

`NotchExportSelectionViewModelTests.BuildSampleTable()` inserts two V21 rows and one V22 row; searching only method bodies for V21 is insufficient. The following **26 methods** involve this fixture or explicit Cv21 selection; the remaining 22 use V22 fixtures/other independent scenarios and are not listed as candidates for deletion.

| Partial file (`UI/ViewModels/`) | Method | Disposition |
|---|---|---|
| `NotchExportSelectionViewModelTests.Basics.cs` (20 methods) | `Collections_ExposeReadOnlyFacade`; `Ctor_SelectsAllRowsByDefault`; `SelectNoneCommand_ClearsAllSelections`; `BuildSelectedTable_ReturnsOnlyCheckedRows`; `Ctor_BuildsIcGroupsAndSelectsFirstPreviewRow`; `SelectPreviewRowCommand_UpdatesPreviewSelection`; `SelectedRowPositionText_TracksVisibleRowIndex`; `GroupCommands_OnlyAffectTargetIcRows`; `SelectPreviewRowCommand_InvokesPreviewCallback`; `SelectPreviewRowCommand_ReinvokesPreviewCallback_WhenSameRowIsSelected`; `CodePreview_UsesBracketOnlyValuesFormat`; `Ctor_WithExportTypeOptions_UsesSelectedType`; `SelectedExportTypeOption_Cv21_PinsVersionSelection`; `SelectedExportTypeOption_Cv21_StillPinsVersionWhenTableHasNoV21Rows`; `Ctor_ReportsPassingToFullCoverageAudit`; `Ctor_ReportsMissingToFullCoverageAudit`; `AttachSimulationSafetyAudit_WhenViolationExists_BlocksExport`; `AttachSimulationSafetyAudit_WhenClean_DoesNotBlockExport`; `AttachSimulationSafetyAudit_WhenPhysicalAuditWarns_ShowsNonBlockingWarning`; `AttachSimulationSafetyAudit_WhenUnavailable_RemainsNotRun` | Convert; use typed V22 rows to preserve multi-IC, selection, preview callback, no-CAD/warning, and safety scenarios. For the two Cv21 methods, add Cv22 pin/empty-table counterparts before deleting the old scenarios; keep the 2 null/empty cases in the last theory. |
| `NotchExportSelectionViewModelTests.Filtering.cs` (3 methods) | `SearchKeyword_FiltersVisibleRows_ByMappingTokens`; `ClearSearchKeywordCommand_ResetsAllViewFilters_ToViewAll`; `SelectedVersionOption_FiltersVisibleRows_AndExportRows` | Convert; preserve filter/reset in the first two. For the third, lock V22 visible/selected export scope first; delete the old assertions only after the version filter is gone in the single-version system. |
| `NotchExportSelectionViewModelTests.RowsAndWorkspace.cs` (3 methods) | `ApplyWorkspaceSelection_BuildsLinkedRows_AndToggleCommandsAffectThoseRows`; `ApplyWorkspaceSelection_DoesNotInvokePreviewCallback_UntilUserClicksRow`; `HidePanelForInspectCommand_InvokesAttachedAction` | Convert; preserve workspace linkage, callback timing, and hide action. |

The following UI service fixtures also contain V21; although the files are under `UI/`, their CI assignment remains `core/uncategorized`:

| Class/source | Method | Disposition |
|---|---|---|
| [NotchExportSelectionProjectionBuilderTests.cs](../../tests/FreeformHelper.Tests/UI/Services/NotchExportSelectionProjectionBuilderTests.cs) (2 methods) | `Build_ProjectsVisibleRowsGroupsAndWorkspaceLinks_FromSingleScope`; `BuildColumnFilterValues_ExcludeTargetFieldFilter_ButKeepSharedScope` | Convert; `BuildProjectionRows` contains V21, so the fixture must be cleaned up even if the current scope selects V22; keep visible/group/workspace/filter scope. |
| [NotchExportSelectionRowSelectionServiceTests.cs](../../tests/FreeformHelper.Tests/UI/Services/NotchExportSelectionRowSelectionServiceTests.cs) (5 methods) | `SelectAll_MarksAllRowsSelected`; `SelectNone_ClearsAllSelections`; `SelectVisibleOnly_KeepsOnlyVisibleRowsSelected`; `SetGroupSelection_AppliesSelectionFlagToGroupRows`; `SetWorkspaceLinkedRowsSelected_OnlyTouchesWorkspaceRows` | Convert; `BuildRows` contains both versions; preserve cross-IC, visible, and linked subset selection. |
| [NotchExportSelectionSummaryProjectorTests.cs](../../tests/FreeformHelper.Tests/UI/Services/NotchExportSelectionSummaryProjectorTests.cs) (2 methods) | `Build_ProjectsScopedCountsAndTexts`; `Build_NoSelection_UsesDefaultExportText` | Convert; `BuildRows` contains V21; preserve V22 scoped counts/summary/empty selection; do not copy mixed-version totals. |

### 2.6 Names Containing Legacy/Q7 That Are Outside the Removal List

- V22 `ShortV22Row_PreserveLegacyCompatibility`/`ShortV22Row_PreserveExactOverflowException` cover the old 9-column V22 input adapter, which is not `LegacyRegularAnchor`. Unless removal of that adapter is separately approved, preserve typed/untyped normalization, overflow, and payload characterization.
- `NotchV22CompensationServiceTests.HasQ7PositiveAllocationInIc_MatchesBuildAllocationsAcrossIcAndQ7Boundary` and `BuildAllocations_UsesAwayFromZeroAtV21ThresholdHalfStep` protect shared allocation admission. Removing V21 output does not permit changing `NotchThresholdQ7Contract` or Q7-positive geometry evidence.
- Legacy in `Compute_ToFullRuleEngineToggle_KeepsLegacyGateResultEquivalent`/`Compute_ToFullRuleTraceEnabledWithLegacyPath_ReportsLegacyTraceStep` refers to the V22 ToFull rule-engine fallback, not `LegacyRegularAnchor`. Matching `LegacyOverlap`, old channel limits/ignored matching values are also outside this scope.
- `ProjectFileMigrationTests.Load_DropsUnsupportedLegacyNotchVersions` tests fallback from `30/31` and `v3.0/v3.1` to V22, not V21 migration; keep it.
- `UiSnapshotPersistenceContractTests` checks the snapshot settable-property contract and already excludes `[LegacyUiSnapshotField]`; it is not a dual-version execution test. Keep the schema guard; update expectations later if an active contract change is approved.

## 3. Disposition of Goldens, Snapshots, and Baselines

“Delete/keep/rebuild baseline” below are recommendations after removal is approved; no private files were read or updated in this work. G2 includes the three golden classes in G3; G6 reruns them through the refactor gate. G0 does not consume data goldens; G1 consumes the expected data for the test selected by its class filter.

### 3.1 Fixed Expectations and Sign-Off Inputs

| Name | V21/Legacy relationship and consuming gates | Disposition and rationale |
|---|---|---|
| `example/BOE36.35/notch_export_v21_current.c` | V21 signed C; G1 drift, G2, G3, G4/3635 script, G6 | **Delete** the active golden/manifest reference; no active consumer remains after V21 output is gone. Preserve old signed provenance in historical documents/existing version records; do not convert it into V22 expected data. |
| `example/BOE36.35/notch_export_v22_current.c` | V22 signed C, but shared formatter metadata still says `Sections: v2.1=empty, v2.2=present`; same gates as above | **Rebuild baseline (only approved removal-related text differences)**: first prove that the original V22 firmware nodes/functions/order remain exact; if removing V21 metadata changes whole-file bytes, obtain sign-off for each item before re-signing. If the approved implementation keeps the entire file unchanged, keep the original signed golden; do not also allow algorithm differences. |
| `example/BOE36.35/notch_export_golden_manifest.json` | Drift test/3635 script reads `outputs.v21`, `outputs.v22` and project/regularMask hashes, bytes, nodes; G2, G3, G4, G6 | **Rebuild baseline**: remove active `outputs.v21` and keep the input lock; update V22 hashes/bytes only for approved changes in the preceding row; nodes should not change because a version is deleted. Do not change input and expected data together to hide drift. |
| `example/golden-snapshots/notch-golden-baseline.json` | `RealProjects_MatchGoldenBaseline` forces both versions; `rowCount`, `versionCounts`, `icCounts`, distribution, first/last, and `firstV21Adjusted` can contain V21; G1, G2, G3, G6 | **Rebuild baseline**: use the V22 projection from before removal as the reference, remove V21 samples/version bucket, and rebuild the V22-only aggregate. Preserve V22 main/no-op samples, ToFull coverage, and 3635/TM8.1 projects; do not delete the entire snapshot. |
| `example/golden-snapshots/tm81-notch-acceptance-matrix.json` | Totals/distribution for both versions, plus V22 no-op, warning, ToFull, diff repair/case contracts; G1, G2, G3, G6 | **Rebuild baseline**: allow only approved shape changes such as dual-version totals/version counts/Legacy distribution; continue comparing V22 case contracts, warning maps, repair, and ToFull against V22 expectations from before removal. |
| `example/BOE36.35/project_3635.json`, manifest `regularMask.path` (public provenance calls it `SeeRegular.csv`; full path not read) | 3635 test/script input; public provenance records both versions enabled and linked threshold; manifest locks project/mask; G2, G3, G4, G6 | **Keep** authoritative input and hashes. The owner must first decide how the single-version system loads old version/mode fields; do not change private input to make tests pass. The mask is shared V22 geometry evidence. |
| `example/TM 8.1/TM8.1.json`, `example/TM 8.1/SeeRegular.csv` | Project/mask names used by the matrix test; the test forces both versions after loading; G2, G3, G6 | **Keep** V22 characterization input; its original content was not read. Change the test to a V22-only request without assuming which legacy fields the project originally contains. |
| Inline snapshots in `NotchTableGeneratorTests`: `ExpectedSubMicroLeftAnchorRows`, `ExpectedSubMicroRightAnchorRows`, and expected rows in the Legacy/dual-version methods in section 2.1 | Explicit V21 rows, Legacy V22 9-column rows, mixed-version row order/count; G1, G2, `application`, G6 | **Rebuild baseline/delete dedicated portions**: extract fixed V22 expectations first, then delete V21 entries/Legacy row snapshots. Preserve the canonical V22 snapshot in `Generate_CadAllocationMode_V22_SnapshotStable_ForSimpleTriangle`. |
| Inline fixtures in exporter/simulation such as `ExpectedV21Q7Boundary*`, `V21CanonicalMainPayload`, `V21CanonicalContinuationPayload`, `V21LegacySamplePayload`, `V21LegacyValues` | Dedicated and mixed-version parity methods in sections 2.1, 2.2; G1, G2, G6 | **Delete** V21 fixtures with no V22 consumer; retain independent V22 expected cells/actions/C in mixed-version methods first; do not automatically convert Q7 fixtures to percent and treat them as ground truth. |
| `tests/FreeformHelper.Tests/Snapshots/ui-visual-minimal-baseline.json` | 9 XAML source hashes; listed sources have no direct V21/Cv21/LegacyRegularAnchor binding and no Firmware payload; G1 UI snapshot, G5, G6 | **Keep**; this baseline does not directly carry V21 payload. Rebuild the baseline entry by entry only if an approved removal actually touches the listed sources; changes to a child view do not automatically change the parent source hash, so do not describe these 9 entries as complete version-option coverage. |
| `tests/FreeformHelper.Tests/Snapshots/ui-rendered-visual-baseline.json` | Rendered hashes for `MainWindow.ConsoleExpanded`, `SettingsWindow.Default`, `HowToUseView.Default`; G1, G5, G6 | **Rebuild baseline (affected surfaces)**: if removing V21 controls/descriptions affects the display, dry-run/review first, then obtain sign-off per surface; keep unaffected surfaces and do not arbitrarily relax dimensions or `maxDistance`. The three existing surfaces do not mean every Step5/Dev page has been previewed. |

Project keys/private actual values in `notch-golden-baseline.json` were not read; the 3635/TM8.1 scope follows the roadmap, and the matrix and workflow table seams follow the test code. `distribution.legacy` is an export-review row classification and must not be equated with computation mode without analysis; whether to delete it or set it to zero after moving to a single version must be determined by the existing classification predicate.

### 3.2 3635 Script Outputs and Measurement Baselines

The script's default output root is `build/perf/3635-regression-latest/`; forward/reverse run directories can also be specified. The following filenames are declared by the code; no existing run was inspected. They are actual/measurement artifacts and cannot replace checked-in signed expectations.

| Name | Consuming gate/V21 relationship | Disposition and rationale |
|---|---|---|
| `notch_v2.1.c`, `runtime-export-c-v21.json` | V21 actual and IPC response from G4/3635 script | **Delete** the V21 export step/artifacts from future runs; do not rewrite historical runs. |
| `notch_v2.2.c`, `runtime-export-c-v22.json` | G4 V22 actual; response contains format/elapsed/path | **Rebuild baseline**: regenerate and compare exactly against the signed V22 golden; elapsed is a measurement and does not require zero diff. |
| `notch_table.csv`, `runtime-export-csv.json` | G4; CSV can contain rows from both versions; budget measures CSV | **Rebuild baseline**: generate using V22-only rows, the retained CSV column contract, and fixed ordering; do not reuse the old dual-version size/row count. |
| `runtime-status-before-exports.json`, `runtime-status.json`, `regression-baseline-summary.json`, `regression-baseline-summary.md` | G4; contains `enableV21`, dual-version order/checks/signedActuals/exports/state | **Rebuild baseline**: retain only the V22 gate/schema; compare the remaining export state before/after; golden checks must be non-skipped and pass=true. |
| `runtime-load-project.json`, `runtime-run-step1.json`, `runtime-run-step2.json`, `runtime-run-step3.json`, `runtime-run-step4.json`, `runtime-ui-instance-status.json` (isolated mode only) | G4 workflow/UI instance diagnostics; load/status may carry old settings or summaries | **Rebuild baseline** for the operational run; preserve semantic expectations for shared geometry/workflow. Do not claim that every response necessarily contains V21; interpret differences using public code/schema. |
| `runtime-selection.json`, `runtime-notch-stage.json`, `runtime-notch.json`, `runtime-notch-validation.json` | G4 query artifacts; may contain versions, row totals/projection diagnostics; some are pure V22 evidence | **Rebuild baseline** for the run/schema; preserve stage/selection/validation consistency for the same V22 result. Do not treat every query output as a zero-diff text file; exclude operational fields such as pid/path/timing. |
| `selection-latency.json` | G4/3635 budget; one fewer version projection may affect timing; no V21 payload | **Keep** the measurement mechanism and remeasure; do not require zero diff for elapsed/p95; retain old records from comparable environments. |
| `docs/performance/regression-baseline-3635.budget.json` | Script budget: selection sample count/total p95/inspector p95/CSV elapsed/C V22 elapsed | **Keep** existing limits. There is no C V21 budget field, so version removal does not require changing numbers; remeasure after the workload changes without relaxing the budget. |
| `docs/performance/regression-baseline-3635.md` | G4 operations/signed provenance/dual-version forward-reverse and performance reference | **Rebuild baseline records**: add approved V22-only operations and results, clearly distinguishing them from the original dual-version measurements; preserve the old sign-off source and comparison evidence. This file is not changed in this work. |
| `docs/performance/3635-selection-export-baseline-2026-03-05.md` | Existing non-isolated historical measurements; not the current gate | **Keep** the historical note; do not promote it to an expectation after removal. |
| `build/perf/3635-notch-generation-benchmark.json`, `build/perf/3635-end-to-end-benchmark.json`, `build/perf/3635-e2e-export/run-*.c` | Opt-in benchmarks; the former helper forces both versions, while the latter follows saved settings; not mandatory G3/G4 goldens | **Rebuild baseline** for V22 workload measurements; confirm the same environment/input/warmup before comparing performance. Old `BuildLegacyRowsElapsedMs` telemetry does not mean that Legacy mode was actually entered. |
| `build/notch-golden-baseline.actual.json`, `build/tm81-notch-acceptance-matrix.actual.json`, `build/notch-example-drift-{version.ToDisplayLabel()}.actual.c` | Actual dumps on G3 failure; may contain V21; G2/G6 may also produce them | **Delete** stale V21-only local actuals; regenerate V22/mixed dumps for diagnostics; never automatically overwrite expected data. |

G0 analyzer/code-size baselines and G5 layout/headless guards are not deleted wholesale because V21 is gone. `UiLayoutGuardTests`, `UiVisualSnapshotTests`, `UiRenderedVisualSnapshotTests`, and `HeadlessUiSmokeTests` continue to use the existing `ui-snapshots`/`ui-stable` entry points (plus local `smoke`).

## 4. Zero-Diff Comparison Targets After Moving to a Single Version

The current requirement remains `V21_before == V21_after` **and** `V22_before == V22_after`; it is not `V21 == V22`. Do not rewrite this gate before removal is approved. After approval, first capture a **V22-only reference** from the source before removal using the same CAD/grid/active mask/layer-filter/compensation settings/V22 threshold/NullValue/profile, then compare it against the result after removal.

If only V21 is deleted, V22 semantic output should remain exact; switching from `LegacyRegularAnchor` V22 geometry rows to canonical CadAllocation changes the contract and cannot be claimed as zero diff. The owner still needs to decide how to reject/read/convert old Legacy projects, with acceptance through a separate set of product behavior tests; a new baseline must not hide conversion differences.

| Gate | Planned wording/comparison target after removal is approved |
|---|---|
| G0 Build/Lint | Maintain build, analyzer, CRLF, and lint standards; this is not numeric zero diff. Do not relax lint or reduce the check scope because there is “one fewer version.” |
| G1 Targeted | V22 callers' current/fresh, typed/untyped, request capture, ordering, sentinel, exception, cache, and audit contracts match V22 characterization from before removal; product behavior that needs to change has separate expectations and decisions. |
| G2 Notch Core | All remaining generator/export/simulation/projection/CLI tests pass; preserve V22 GCC/C# exact parity, shared geometry/allocation, and single-result projection. Test-count reductions must correspond to the method/case list; do not use old case counts as a hard gate. |
| G3 Golden | V22 actual C against the **approved V22 golden**; V22-only 3635/TM8.1 snapshots against the **V22 subset captured before removal and the approved single-version schema**. Do not compare against dual-version aggregates or actuals produced by the same run. |
| G4 Runtime CLI | C V22 from isolated UI/IPC against the same G3 signed golden, consistent with the Application production-equivalent seam. Preserve production selected layers/active mask. Protect output and state invariance with cold/warm sequences of CSV→C V22, C V22→CSV, and repeated C V22, replacing cross-version export order. |
| G5 UI | Unaffected XAML hashes/surfaces keep existing baselines; surfaces with approved removal of version controls are compared against the **reviewed and approved V22-only UI baseline**. Rendered guards retain existing hash distance rules; do not claim literal zero diff for all pixels; layout, theme, and Dev preview/headless guards remain in effect. |
| G6 Merge | Maintain full lint + refactor gate + UI snapshots; reference the new V22-only expectations and product-change sign-off records. `uncategorized` must still cover service/benchmark classes not listed in named groups. |

C comparison retains the existing normalization: CRLF/LF/lone CR are normalized to LF, with at most one optional EOF newline; other whitespace, comments, and ordering remain ordinal exact. Raw SHA-256/bytes/nodes in the manifest are a separate strict check that remains and must not be skipped because normalized comparison passes.

Deleting V21 text in `Sections`, version controls, or mixed-version aggregates are expected differences that can be enumerated; first prove against the old V22 reference that there are no other differences, then approve the new expectations. Section 3.1 of the current roadmap allows only the completed R13.002 provenance correction; this document adds no exceptions. On golden failure, still preserve the actual diff first; do not enable update environment variables to make it pass.

## 5. Order: Preserve Coverage Before Deleting Tests

This is a dependency order, with no date, release version, or instruction to “start now.” V21/Legacy tests and gates remain until the prerequisites for each step are met.

1. **Confirm product boundaries first.** The owner must separately authorize removal and decide how to handle old projects' V21-only/both/Legacy+V22 settings and old-format input, as well as visible differences in V22 C comments/CSV schema/RuntimeQuery status. Preserve the original signed references first; do not implement migration, adapters, or new extension points in advance.
2. **Lock single-version V22 results first.** Establish the characterization below within the existing test classes/fixture mechanisms, preserving V22 expectations from before removal; do not infer expectations from new actuals. Existing equivalent coverage only needs to be explicitly preserved; duplicate tests are not required.
3. **Separate mixed-version fixtures/theories first.** First replace the UI fixtures in section 2.5 with typed V22, preserving the original selection/filter/preview/safety scenarios; remove only V21 portions from exporter sentinel theories, the simulation INT16 theory, and generator callback timing theories, preserving shared cases. Updating G3 single-version expectations requires sign-off; do not delete golden classes first.
4. **Then delete V21-only assertions/methods.** After the corresponding V21 output is removed and all V22 characterization passes, delete methods dedicated to payload Q7/V21 GCC/projected simulation and fixtures with no consumers. Preserve shared Q7-positive allocation, INT16, UINT16, and V22 compatibility input tests.
5. **Handle Legacy methods separately and last.** Delete tests for Legacy V22 geometry rows, dispatch/freezing, request-specific cache, scenario legacy apply, and saved old modes only after the LegacyRegularAnchor computation/read policy is also removed. Preserve independent coverage for normal batch lease/row-count, V22 orientation/eligibility, and project loading/roundtrip first. Removing the V21 exporter alone is insufficient.
6. **Clean up gate dependencies and active baseline references last.** Remove V21 C paths/manifest entries/script steps/artifacts only after V22 G2/G3/G4 are established, and rebuild affected aggregate/metadata/UI expectations. When deleting or renaming a class, update the existing `run-tests.ps1` lists at the same time to avoid stale-name validation or reduced CI coverage; keep all V22 golden classes. Full verification still uses G6; do not claim removal is complete based only on class-filtered unit tests.

### 5.1 V22 Characterization That Must Exist First

| Contract | Existing evidence that can be preserved | Coverage to complete/separate before deletion |
|---|---|---|
| Canonical geometry/matching/orientation | Generator `Generate_CadAllocationMode_V22_*` (source/target, cross-IC, ToFull, models/active set); typed V22 projection tests | XWay/YWay/XYWay eligibility, matched/unmatched CAD, geometry fallback, and fixed V22 anchors/rows for the same CAD spanning multiple regulars; do not borrow Legacy row semantics as expectations. |
| Request capture/final admission/determinism | `Generate_CadAllocationMode_FreezesProjectionThresholdAtCandidatePhaseStart`, `SnapshotsCadOutputFwDiffMapAfterProfiles`, `FinalThresholdFiltersRowsAndCoverageAuditFromSameCandidateView` | Convert mixed-version freeze-resolution/request, submicro snapshots, and five-run determinism to V22; preserve every callback timing, threshold admission/overflow, and row-count/audit consistency. |
| Firmware ABI/numeric/sentinel | Exporter V22 GCC parity, continuation, INT16 truncation, typed/untyped, flags, per-IC dispatch, FwBaseMask, Release no-op tests | V22 signed percent/combine boundaries, self-target, multiple-term writeback/wrap, nonfinite/out-of-range baseline; retain exact V22 assertions in dual-version sentinel theories. A pass from an early return when GCC is unavailable is not evidence of compiled parity. |
| Simulation/review/safety | `Simulate_WhenV22RowExists_TransformsAnchorAndTargetUsingCombinePayload`, `Simulate_WhenV22ContinuationRowsExist_MergesAllLegsIntoSingleAnchorAction`, V22 review, scenario/copper replay | Explicitly preserve V22 cells/actions/legs from mixed continuation, the INT16 audit baseline, and missing-diff diagnostics; V21 coverage cannot replace V22 cell/action/EMS/global-flow consistency. |
| Cache/single result/stale completion | The two VM Release↔Debug profile cache tests, `ExportNotchCommand_TargetCoverageRequestReusesResolvedBatchAndMatchesFreshProjection`; completion/currentness tests in the `NotchGenerationStaleCompletion` partial; cache fault/retry/epoch/collision tests | Convert the V21-only threshold-reproject test to V22: warm/fresh exact, zero-row reuse, sentinel/final-only change, computation rebuild; retain V22-only output state cases in the resolved identity test; do not delete normal row-count lease along with the legacy cache half. |
| Export selection/presentation | Existing V22-only transfer/no-op/no-CAD/filter tables and shared scenarios in section 2.5 | Cv22 pinning, no V22 rows/empty selection, CSV/C V22 naming, selection/export scope, workspace preview callback, blocking EMS/nonblocking physical audit; replace Cv21 tests first. |
| Settings/project/RuntimeQuery | `ApplyViewSnapshot_Cv22ExportType_SelectsCv22`, unknown-property/collection isolation/V22 fallback tests | V22-only default, native threshold validation, V22 save/load roundtrip, state restoration after both success and failure with an accepted V22 format; expectations for old V21/Legacy projects must be decided by the owner first. App settings isolation/deferred flush and Ctrl+S/toast are outside the removal scope. |
| Production provenance/snapshots | V22 drift theory, signed manifest, TM8.1 V22 case contracts, G4 isolated IPC | Capture V22-only snapshots under the same production filter/mask first; accept metadata differences separately from firmware semantic differences. G3/G4 private data revalidation is performed by an authorized commander/owner; a missing-data skip does not count as a pass. |

## 6. Verification in This Work and Pending Decisions

This work verifies only the planning document and class tests that can run in the sandbox without reading private data; it does not claim completion of G2–G6 removal acceptance.

- `dotnet build tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --no-restore -p:UseAppHost=false -p:UseSharedCompilation=false --nologo -m:1 -nr:false`: succeeded; includes the UI project, 0 warnings/0 errors.
- 4 runs of `dotnet test ... --no-build -p:UseAppHost=false --nologo --filter "FullyQualifiedName~<Class>"`: `NotchSettingsTests` 12, `NotchApplySimulationServiceTests` 12, `NotchApplySimulationReviewUseCaseTests` 2, `SimulationWorkspaceUseCaseTests` 3; total 29 passed/0 failed/0 skipped.
- Document checks confirmed 23 classes/100 relevant methods; all 105 full method-name references exist; CRLF has no bare LF; `git diff --check` passed. `pwsh -NoProfile -File ./scripts/verify.ps1 -StructureOnly` passed: 56 listed classes, 12 workspace ownership cases, 13 build-output selection cases, CRLF normalized=0, XAML issues=0; temporary report removed.
- Used the existing prepare script before build/test, skipping process stop/workspace-wide normalization; the specified document was written separately with CRLF. The sandbox does not run lint, process listing, restore, submodule update, G3/G4 private data gates, full-project tests, commit, push, git config, or network operations; no additional report file was created.

Pending owner decisions are removal authorization, handling of old projects/compatibility input, and the scope of visible format/schema/UI differences and golden re-signing; these do not prevent delivery of this inventory-only work and do not mean any removal version or schedule has been decided.
