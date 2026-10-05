# V21／Legacy-specific production code removal slice plan

This document only plans the production code slices for the TODO item “V21／Legacy complete removal”. The owner's decision 「先保留」 (keep it for now) remains in effect; none of the slices below is authorized to begin. No code has been removed in this work, and no execution date or target version is proposed. Existing V21／LegacyRegularAnchor behavior and the gates protecting it remain unchanged.

The code baseline is `7477b07d3969ad6ad15960d0d1ac8b60ef37a9bc`. `AGENTS.md`, `docs/agents/domain.md`, the dependency graph, the current roadmap／TODO, the Notch reference, and the Runtime CLI contract were read first, followed by targeted tracing of `src/` and related tests; `example/` was not read. The ProjectReference entries in the dependency graph match the current five projects, so regeneration was unnecessary. Contract entry points: [Notch reference](../reference/notch-system-reference.md), [Runtime CLI](../reference/runtime-cli-plan.md), [current owner decisions](../../TODO.md). Removability in this document is based on current callers／branches, and cannot be determined solely from the names `Legacy`, `Compatibility`, or `Q7`.

## Scope, prerequisites, and the meaning of zero diff

The scope is generator, firmware projection／evaluation, export, runtime adapter, cache, and settings／export UI code that exists solely because of `NotchAlgorithmVersion.V21` or `NotchComputationMode.LegacyRegularAnchor`. Persistence, test retirement／additions, and golden data are covered by other briefs; this document only lists the gates they must provide, without designing migration or specifying how to delete tests or goldens.

All removal slices share prerequisite **P0**: the owner must separately and explicitly authorize retirement of these two behaviors and confirm how unsupported public generator／simulation requests, CLI formats, and old project inputs will be handled. P0 is currently unmet; the table below must not be treated as authorization to execute. LegacyRegularAnchor requests must not silently be computed as CadAllocation, nor V21 requests as V22.

**P1 (the persistence delivery gate in a separate effort)**: handling of old values, version／mode enums, and stored export types is complete; production requests used by this plan are indeed V22／CadAllocation. Before UI fields are deleted, the separate effort must first remove references to those fields from settings load, apply, snapshot, and save code, retaining types／members still used by the current slice so it continues to compile. This plan does not delete the serialized members `NotchAlgorithmVersion.V21`, `NotchComputationMode.LegacyRegularAnchor`, or `NotchExportFileType.Cv21`, or change enum values. In particular, `Cv22` currently has an implicit ordinal; directly deleting the preceding `Cv21` would change its value.

**P2 (the V22 threshold delivery gate in a separate effort)**: for every retained V22 request, the effective threshold must be exactly equal before and after removal. `NotchTableGenerator.Generation.Thresholds.cs:ResolveEffectiveV22ThresholdPercent` uses `ThresholdQ7 * 100.0 / 128.0` when Link is enabled, rather than `ThresholdPercentV22`; the UI rounds the displayed percent to two decimal places. With Q7=1, for example, the generation threshold is 0.78125%, while the UI displays 0.78%; the latter cannot replace the former. `OnNotchThresholdPercentChanged` quantizes back to Q7 when Link is enabled; merely deleting the callback would change the threshold produced by the same subsequent input action. P2 must provide characterization／disposition evidence for effective values and editing behavior; without this evidence, retain Q7／Link consumers and stop the affected slices.

**P3 (the test delivery gate in a separate effort)**: before each slice begins, its V22 characterization must already exist and be executable; assertions／compile-time references for retired behavior must be handled in coordination with the test brief. Passing results must not be obtained by skipping V22 cases or updating expected output. The test seams named below already exist; missing items are explicitly marked “characterization needed”. No tests are added in this work.

Zero-diff comparison uses the same CAD／grid, active regular set, CAD output FW diff map, compensation settings, effective threshold, sentinel, and export profile, comparing V22 rows (including order, Values, V22Node, identity, and comment), coverage audit, simulation Cells／Actions／diagnostics, and raw CSV／C output item by item. Cold／warm／concurrent cache paths must produce identical results; elapsed timing need not be exact. UI／protocol text changes that remove V21 options, Legacy badges, or CLI support are explicit changes required to retire the features; they do not permit changes to V22 payloads, ratios, or C comments. For a table that originally generated both versions, compare only its V22 projection and V22 export; V21 rows need not remain after removal.

## Shared code retention boundaries

None of the following can be counted as V21／Legacy-specific deletions; there is currently no evidence supporting their deletion for this goal.

| File: symbol | V22 consumer relationships shown by the code | Deletion count and gate／risk |
|---|---|---|
| `src/FreeformHelper.Application/Services/NotchAllocationService.cs:BuildAllocations, HasQ7PositiveAllocationInIc, TryComputeAllocation, NotchAllocation`; `NotchThresholdQ7Contract.cs:EncodeFraction` | CadAllocation profiles／memo and UI per-IC pool admission use Q7-positive allocation; Q7 here is not a V21 firmware payload. | **0 lines**; lock down the Q7=0／positive boundary and cross-IC pools. Deleting Q7 would change anchor／pool membership; high risk. |
| `src/FreeformHelper.Application/Services/NotchV22CompensationService.cs:CreateContext, Compute` and its partials; `NotchV22CompensationService.Stages.cs:BuildCompensationAllocations, RunStageCEvaluateToFullAndDiagnostics` | `NotchTableGenerator.Generation.V22.cs:BuildV22CadCandidate` consumes the full context; the decision adapter for `EnableToFullRuleEngine=false` remains a path supported by V22 settings. The multi-parameter Compute is a compatibility API and does not dispatch by V21／LegacyRegularAnchor. | **0 lines**; `NotchV22CompensationServiceTests`. Do not interpret rule-engine fallback or legacy comments on reachability helpers as LegacyRegularAnchor; high risk. |
| `src/FreeformHelper.Application/Services/NotchV22TargetAllocationService.cs:Build`; `NotchV22TargetAllocationPolicy:ResolveAreaMode, ProjectTargetCoverage, ProjectCompatibilityDisplay` in the same file | CurrentGain／ConservativeNoGain use target coverage; Disabled still resolves to SourceAreaDominant and uses compatibility display and source-area legs. | **0 lines**; `NotchV22TargetAllocationServiceTests.TargetAllocationPolicy_MapsEveryCompensationModelToItsCoveragePath`, `ProjectTargetCoverage_CompatibilityPathsPreserveAllTargetDisplayRatio`. Deleting the fallback would change Disabled V22; high risk. |
| The three overloads of `src/FreeformHelper.UI/Services/NotchDisplayProjector.cs:Build`, `IsEffectiveTarget`, and the target-coverage fallback | Inspector, Pad Info, Notch Detail, and Runtime Query share V22 compensation／allocation projection; this file has no version／computation-mode dispatch. Nullable target coverage and strict-only display have existing tests. | **0 lines**; `NotchDisplayProjectorTests.Build_WhenTargetCoverageIsCompatibilityMode_PreservesStrictOnlyDisplay`. Retain all three input seams; deleting the fallback would change diagnostics; high risk. |
| `src/FreeformHelper.Application/Export/NotchTableExporter.cs:ExportAsCsv, ExportAsCInitializer`; `src/FreeformHelper.Application/Services/NotchV22FirmwareProjector.cs:Project` | The fixed nine CSV columns form the review schema; the V22 formatter and simulation share the projector, retaining typed／untyped／short row compatibility behavior. | **0 lines**; `NotchTableExporterTests.ExportAndSimulation_TypedAndUntypedV22Rows_NormalizeIdentically`, `ExportAndSimulation_ShortV22Row_PreserveLegacyCompatibility`. Do not equate a missing V22Node with V21. |
| `src/FreeformHelper.UI/Services/NotchDetailUseCase.cs:BuildFromResolvedResult`; `src/FreeformHelper.UI/ViewModels/NotchDetailViewModel.cs:ThresholdQ7, IsBelowThreshold` | Detail for a V22 resolved result still displays and compares the Q7 threshold; these consumers do not serve V21 alone. | **0 lines** (before P2); `NotchDetailUseCaseTests` plus effective-threshold characterization. Changing to percent is a separate behavior change and must not be slipped into this plan. |

Short filenames above, except in the first row, refer to the same Services directory; this table is a retention audit, not a set of fake implementation slices with zero deletions.

## Acceptance codes and line-count conventions

- **B**: every slice must pass a UI build, the corresponding test project build, and `git diff --check`; run the repo-required lint in a normal execution environment, with 0 errors／warnings and no relaxed analyzers. Use the existing workspace prepare; do not list processes in the sandbox, use `-SkipStopApp -SkipNormalizeLineEndings`, and write the sole document in this work as CRLF directly. Actual code changes must still follow the repo prepare rules.
- **C-G**: generator V22 rows／coverage／freeze characterization. Existing cases in `NotchTableGeneratorTests`: `Generate_CadAllocationMode_V22_SnapshotStable_ForSimpleTriangle`, the three compensation-model cases, `Generate_CadAllocationMode_V22_UsesCadOutputFwDiffAsSource_AndKeepsRegularFwDiffAsTarget`, `ProjectCadAllocationResolvedBatch_FinalThresholdFiltersRowsAndCoverageAuditFromSameCandidateView`, and `Generate_CadAllocationMode_FreezesProjectionRequestAtCandidatePhaseStart`. Additional exact characterization is needed for V22 eligibility with None／XWay／YWay／other freeforms, empty overlaps, and missing match／geometry fallback.
- **C-E**: V22 typed／untyped／short rows, Release no-op filtering, pre-filter sorting, multiple ICs／mask／sentinel／continuation, and V22 GCC runtime parity in `NotchTableExporterTests`. Additional exact before／after characterization is needed for Release／Debug CSV／C raw bytes from the same input, including all C comments and newlines. An absent GCC must not be treated as verified parity.
- **C-S**: the V22 case of `NotchApplySimulationServiceTests.Simulate_WhenFrameIsOutsideInt16Domain_UsesFirmwareBaselineForCellsAndAudit`, and V22 main／continuation／mean／duplicate diff／row-column order cases; non-finite values, fractions, and INT16 upper／lower bounds must all be exact.
- **C-U**: `NotchExportSelectionViewModelTests` and related projection／summary／quick-filter tests; selection／payload／preview characterization is needed for V22-only transfer／no-op／no-CAD rows. Existing untyped V22 display fallback remains protected.
- **C-R**: `RuntimeQueryUseCaseTests`; characterization is needed for the shared command seam that successfully exports V22／CSV, restoration of delegates／file type on failure, state invariants, and consistency between V22 pad／notch queries and display. Existing versioned-C “workflow not ready” cases cannot replace successful-export evidence.
- **C-K**: `NotchExportGenerationCacheServiceTests` and the NotchExportCache partial cases in `FreeformHelperViewModelTests`; lock down warm exact output, empty batches, task joining, fault retry, fingerprint collisions, selected sparse reuse, and refusal to publish stale completions for epoch／final revision／source revision.
- **C-T**: `NotchSettingsTests` and existing settings change／draft／ViewModel seams; P2 must add V22 characterization for linked／unlinked values, editing and Save／Discard, undo, and downstream invalidation.
- **U**: `UiLayoutGuardTests`, corresponding headless smoke tests, and Dev page／Settings／export window previews; only retired controls disappear, and token／theme support and V22 controls remain usable. Do not introduce a new visual language, inline dimensions, or colors for this work.
- **E**: for slices affecting generator／export／VM／runtime results, a separate effort must provide the existing V22 production-equivalent exact export／golden gate; this document does not read or change private data. The test brief must first decide how to handle the retired portion of the current dual-version gate; do not simply bypass V21 failures and then declare E passed.

Line counts are **production C#／AXAML physical lines** (including comments and blank lines), excluding docs, tests, goldens, and generated code. Whole-file counts were measured from baseline files; all ranges are **estimates**, and the actual net diff must be calculated during implementation. For shared helpers moved into existing classes, subtract the lines added there from the deletion count; replacement text does not count as a net deletion. Do not count lines twice across slices or estimate DLL size or duration.

## Suggested slice order

The numbering is a topological order in which each slice can be built individually, not a schedule. Each item must also satisfy P0 and P3; P1／P2 are additional blockers only where explicitly listed. Temporarily retaining an internal helper with no callers until its file's deletion slice avoids large atomic changes across files; do not add adapters, configuration, extension points, or generalization layers.

### S01: Retire the V21-specific Runtime Query surface

- **File: symbol**: remove the `c-v21` mapping／normalization from `src/FreeformHelper.UI/Services/RuntimeQueryUseCase.Commands.Export.cs:QueryExportNotchAsync` and simplify the accepted-format text; V21 example／format text in `RuntimeQueryUseCase.cs:QueryHelpExamples, QueryHelp`; remove `notchExportState.enableV21` from `RuntimeQueryUseCase.Commands.TerminalStatus.cs:QueryStatus`. The latter two files are in the same UI Services directory.
- **Prerequisites**: P0, C-R; obtain the retirement contract for status fields／help／formats first. `c-v21` returns `INVALID_ARGUMENTS` through the existing unsupported-format branch and must not fall back to V22.
- **Acceptance**: B, C-R, E; V22／CSV still go through `RuntimeQueryUseCase → ExportNotchCommand`, `finally` restores the three transient delegates／file type, and current export write／progress／summary side effects remain.
- **Net deletions**: estimated **5～10 lines**.
- **Risk**: medium; CLI and status consumers are public surfaces. Explicit user-visible change: V21 format／help／status fields are retired; V22 calculation and other query payloads remain unchanged.

### S02: List only the existing V22／CSV formats in the export UI

- **File: symbol**: remove the Cv21 option from `NotchExportFileTypeOptions` in `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Core.cs:FreeformHelperViewModel`; remove Cv21 branches from `NotchExportFileTypeMetadata.cs:IsCExportType, GetExportKindLabel`. Use the existing option／pinned-version mechanism without redesigning the selection model.
- **Prerequisites**: S01, P1's stored Cv21 handling gate, C-U／C-R. Retain the `FreeformHelperViewModel.Models.cs:NotchExportFileType.Cv21` declaration until the enum／persistence brief is complete; do not reorder the Cv22 ordinal.
- **Acceptance**: B, C-U, C-R, E, U; V22 pinned selection, CSV selection, filenames, and cancel／preview side effects remain the same.
- **Net deletions**: estimated **3～8 lines**.
- **Risk**: medium; old saved selections must not fall into an unintended default when the option is absent. Explicit user-visible change: the C v2.1 option disappears.

### S03: Remove LegacyRegularAnchor orchestration from the generator, first resolving the eligibility dependency

- **File: symbol**: remove legacy dispatch from `src/FreeformHelper.Application/Services/NotchTableGenerator.cs:Generate, EvaluateCadRowEligibility`; Generate uses the existing CadAllocation request validation to reject non-CadAllocation requests, and P1 must likewise verify that the eligibility entry point rejects retired modes; delete `NotchTableGenerator.Generation.cs:GenerateLegacyRegularAnchor, CaptureLegacyNotchGenerationRequest, CanBuildLegacyRow, BuildLegacyRow`; delete `NotchTableGenerator.Eligibility.cs:EvaluateLegacyEligibility`; for retained V22, `EvaluateCadAllocationEligibility` uses the exact predicate from the existing `V22LegacyRowStrategy.CanHandle` (CAD is non-null and XWay／YWay), without retaining a caller of that strategy; delete `NotchTableGenerator.Generation.Allocation.cs:BuildCadMaxAllocation`; delete `NotchTableGenerator.Generation.Thresholds.cs:PassesThresholdForVersion(..., LegacyNotchGenerationRequest)`. All short filenames are in Application Services.
- **Prerequisites**: P1's valid-mode request gate, C-G, especially the eligibility characterization still needed; S03 can still generate CadAllocation V21 projection, which is left for S07.
- **Acceptance**: B, C-G, E; V22 eligibility reasons, estimated counts, and anchors remain identical item by item; the four normal generation phases, callback freeze boundaries, and active-set／output-map snapshots do not move. ToFull emission for non-freeform anchors remains handled by the canonical generator; do not add a generation gate based on the eligibility X／Y predicate.
- **Net deletions**: estimated **200～240 lines**, excluding the next three file-deletion slices.
- **Risk**: high; normal eligibility currently still calls `CanBuildLegacyRow`, so directly deleting the strategy would fail compilation; merging the two modes would change old request semantics. Remove branches only for modes whose retirement has been authorized.

### S04: Delete the V21 regular-anchor algorithm file

- **File: symbol**: delete the entire `src/FreeformHelper.Application/Services/NotchAlgorithms/V21NotchAlgorithm.cs:V21NotchAlgorithm` file (including `CanHandle, Build, FillAxis`).
- **Prerequisites**: S03; all production callers have stopped using this class, and C-G's V22 cases remain.
- **Acceptance**: B, C-G, E; targeted reference searches show no production callers. Retain the Q7 codec still used by final V21 projection.
- **Net deletions**: measured whole-file count of **174 lines**.
- **Risk**: low (once prerequisites are met); do not also delete Q7 used by V22 allocation.

### S05: Delete the V22 legacy geometry-row strategy

- **File: symbol**: delete the entire `src/FreeformHelper.Application/Services/NotchAlgorithms/V22LegacyRowStrategy.cs:V22LegacyRowStrategy` file (`CanHandle, Build`).
- **Prerequisites**: S03 has retained the exact predicate for normal eligibility in the existing eligibility method; C-G.
- **Acceptance**: B, C-G, E; canonical V22 seven-column nodes, continuations, and target coverage remain identical; the nine-column legacy geometry rows in this file have no remaining production callers.
- **Net deletions**: measured whole-file count of **232 lines**.
- **Risk**: medium; the “V22” filename can still be mistaken for the canonical algorithm; do not delete valid untyped row compatibility behavior in `NotchV22Node`／the firmware projector.

### S06: Reclaim private support code for the two legacy builders

- **File: symbol**: delete the entire files `src/FreeformHelper.Application/Services/NotchAlgorithms/NotchAlgorithmHelpers.cs:NotchAlgorithmHelpers`, `NotchAxisContext.cs:NotchAxisKind, NotchAxisGeometryContext, NotchAxisNeighborContext`, and `LegacyNotchGenerationRequest.cs:LegacyNotchGenerationRequest`.
- **Prerequisites**: S04, S05; targeted queries for all the types／helpers above confirm that only declarations and mutual references remain; C-G.
- **Acceptance**: B, C-G, E; do not reclaim `NotchTableGenerator.Generation.Allocation.cs:SelectCadAllocationAnchor` or V22 compensation geometry.
- **Net deletions**: measured whole-file count of **210 + 85 + 10 = 305 lines**.
- **Risk**: low (after callers are cleared); the geometry／neighbor contexts in these three files differ from the geometry tools used by V22 compensation.

### S07: Stop projecting V21 rows from CadAllocation

- **File: symbol**: delete `src/FreeformHelper.Application/Services/NotchTableGenerator.Generation.V22.cs:ProjectV22RowsToV21Rows` (including local `FillLegacyLeg`); remove ExportsV21／ThresholdQ7 from `NotchTableGenerator.Generation.cs:CadAllocationProjectionRequest` and the V21-only payload／admission branches from `CaptureCadAllocationProjectionRequest, BuildCanonicalProjectionCandidateView, AppendCanonicalExports`; remove V21 dispatch from `NotchTableGenerator.Generation.Thresholds.cs:PassesV21ThresholdQ7, PassesThresholdForVersion`, with V22 eligibility using the existing percent gate.
- **Prerequisites**: S03, S02, P1's enabled-version request gate, C-G; **retain the Link／Q7 conversion in `ResolveEffectiveV22ThresholdPercent` until P2／S15**.
- **Acceptance**: B, C-G, E; the V22 projection of V22-only and formerly mixed requests remains exact; rows／audit come from the same admitted candidate view; guard／cap and empty projection remain unchanged; candidate context and resolution fingerprint／memo stay untouched.
- **Net deletions**: estimated **85～115 lines**.
- **Risk**: high; V21-only requests currently use Q7 admission, while mixed requests prioritize V22 admission; neither can simply replace all old requests. V22 linked thresholds also still depend on Q7.

### S08: Remove the V21 formatter from the firmware C exporter

- **File: symbol**: remove orderedV21Rows／V21 dispatch and the retired mixed-version branch from `src/FreeformHelper.Application/Export/NotchFirmwareCExporter.cs:Export`; delete `ExportV21FwFile, AppendV21TypeBlock, AppendV21Tables, AppendV21FunctionBlock, FormatV21NodeLine, FormatV21TypeLiteral, FormatDiffLiteral(int,int)`. Retain the V22 overload `FormatDiffLiteral(int?)` and shared IC dispatch／mask／header.
- **Prerequisites**: S02, C-E; P0 has delivered handling of unsupported V21／mixed inputs. This can run before S03／S07 because the V22 C selection in UI／runtime already uses the existing pinned mechanism.
- **Acceptance**: B, C-E, E; V22 output from `AppendGenerationMetadata` must still preserve `Sections: v2.1=empty, v2.2=...` byte for byte. The v21Count parameter may be simplified to the existing fixed empty text; the line cannot be deleted because the feature is removed. Retain pre-filter sort, Release no-op pruning, Debug mask, sentinel snapshot, and encoding／newline.
- **Net deletions**: estimated **255～280 lines**.
- **Risk**: high; V22 C bytes include metadata that appears to be V21-specific, and the dedicated C function block also calls shared formatters; do not delete shared symbols along with the entire block.

### S09: Remove V21 apply from simulation while retaining the V22 INT16 baseline in the same slice

- **File: symbol**: delete the V21 branch／contract text／actions builder from `src/FreeformHelper.Application/Services/NotchApplySimulationService.cs:Simulate, BuildV21Actions`; move `NotchV21FirmwareEvaluator.cs:QuantizeToFirmwareInt16` unchanged into the existing `NotchApplySimulationService` and redirect V22 calls to this helper; delete the remainder of `NotchV21FirmwareEvaluator.cs:NotchV21FirmwareEvaluator, NotchV21FirmwareEvaluation`. Delete `NotchApplySimulationModels.cs:NotchApplySimulationAction.IsLegacyApproximation` once it has no production callers and P3 has handled test references.
- **Prerequisites**: S07, C-S, C-E; V21 simulation requests have been retired under P0. Retain the existing unsupported-version response without creating another simulation path.
- **Acceptance**: B, C-S, C-E, E; quantization remains finite truncate→INT16 saturation, NaN／Infinity→0, with modular writeback and V22 continuation evaluation unchanged; before baselines for Cells, Actions, and audit remain exactly identical.
- **Net deletions**: estimated **205～230 lines** (the measured evaluator whole-file count is 110 lines, minus the lines added by moving the quantization helper).
- **Risk**: high; deleting the evaluator based solely on its filename would break V22. Leave `NotchApplySimulationRequest.ComputationMode` and UI session propagation fields for P1; do not remove parameters across multiple APIs in this step.

### S10: Delete the V21 final projector and node models with no consumers

- **File: symbol**: delete the entire `src/FreeformHelper.Application/Services/NotchV21FirmwareProjector.cs:NotchV21FirmwareProjector, NotchV21FirmwareProjection, NotchV21ProjectedSourceRow, NotchV21ProjectedLeg, NotchV21FirmwareNode` file.
- **Prerequisites**: S08, S09; exporter／simulation production references to all the types above have been cleared, C-E／C-S.
- **Acceptance**: B, C-E, C-S, E; both V22 exporter／simulation continue to use `NotchV22FirmwareProjector.Project`; no new projector or recalculation.
- **Net deletions**: measured whole-file count of **339 lines**.
- **Risk**: low (once prerequisites are met); do not delete shared sentinel validation or V22 typed/untyped normalization.

### S11: Delete the final V21 payload Q7 codec with no consumers

- **File: symbol**: delete the entire `src/FreeformHelper.Application/Services/NotchV21Q7Codec.cs:NotchV21Q7Codec` file.
- **Prerequisites**: S04, S07, S08, S09, S10; production codec references are cleared, C-G／C-E／C-S.
- **Acceptance**: B, C-G, C-E, C-S, E; retain `NotchThresholdQ7Contract`, `NotchAllocation.Q7`, the Q7-positive pool predicate, and compensation allocations.
- **Net deletions**: measured whole-file count of **63 lines**.
- **Risk**: low (once prerequisites are met); payload Q7 and allocation／threshold Q7 serve different purposes.

### S12: Remove request-specific legacy table branches from VM generation and cache

- **File: symbol**: remove legacy fingerprint selection, `legacyTableToStore`, TryGet／Generate fallback, and TryStore branches from `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchTableGeneration.cs:GenerateCurrentNotchTableAsync`, retaining the existing CadAllocation batch owner; delete the old generation fingerprint with no callers in `FreeformHelperViewModel.Persistence.NotchExport.Helpers.cs:ComputeNotchExportSettingsFingerprint` (not the persistence schema); delete the table overloads of `src/FreeformHelper.UI/Services/NotchExportGenerationCacheService.cs:TryGet`, `Store` overloads, and `TryStore, TryStoreEntry, CacheEntry.Table`, simplifying only matching helpers used by the table API if necessary.
- **Prerequisites**: S03, S07, P1's mode gate, C-K. `TryGet/Store` are public APIs; the caller inventory and P0／P3 must explicitly cover them. “Only tests reference them” does not automatically justify breaking compatibility.
- **Acceptance**: B, C-K, C-G, C-S, E; empty batches remain cacheable; concurrent export／simulation join the same resolution task and each performs its own final projection; fault retry, identity equality, epoch／revision stale refusal, busy scope `finally`, and sparse promotion remain.
- **Net deletions**: estimated **210～280 lines**, counting only table storage/API and VM fallback, without deleting the entire batch matching／signature／metrics group.
- **Risk**: high; the two cache types currently share `_entry`／lookup metrics, so mechanical deletion of table branches can easily change batch hit／miss behavior or publication timing for old completions.

### S13: Retire non-V22 status／filters in the export row-selection UI

- **File: symbol**: simplify `src/FreeformHelper.UI/ViewModels/NotchExportSelectionViewModel.Models.cs:NotchExportRowDisplayMode.LegacyOnly, NotchExportRowItemViewModel.IsStatusLegacy, BuildMappingStatusText, BuildStatusText`; simplify the Legacy option／`IsFilterLegacyOnly, LegacyRowCount` in `NotchExportSelectionViewModel.cs` and related notifications in `NotchExportSelectionViewModel.Filters.cs:NotifyFilterStateChanged` and `NotchExportSelectionViewModel.Selection.cs:UpdateCounts`; remove legacy-only consumption from `src/FreeformHelper.UI/Services/NotchExportSelectionProjectionBuilder.cs:MatchesDisplayModeFilter`, `NotchExportSelectionQuickFilterService.cs:StatusFilterCycle`, and `NotchExportSelectionSummaryProjector.cs:Build, NotchExportSelectionSummarySnapshot.LegacyRowCount`; remove Legacy filter／badge bindings from `src/FreeformHelper.UI/Views/NotchExportSelectionWindow.axaml`.
- **Prerequisites**: S07, C-U; all incoming rows are V22. The predicate for `IsStatusLegacy` is version not equal to V22, **which differs from `V22Node is null`**.
- **Additional consumer**: `src/FreeformHelper.UI/Views/NotchExportSelectionRowsPaneView.axaml:178` still binds `Classes.legacy` to `IsStatusLegacy` (used by the export window); remove this binding when reclaiming the property, or an invalid runtime binding will remain.
- **Acceptance**: B, C-U, E, U; V22 transfer／no-op／no-CAD selection, sorting, workspace preview, and payload text remain identical; retain all Values／flags／analysis fallbacks for `V22Node is null`. Do not require a node merely to delete the “Legacy payload” string.
- **Net deletions**: estimated **45～75 lines**.
- **Risk**: medium; V22 rows without typed nodes remain valid. Explicit user-visible change: Legacy filter／badge／summary items disappear, and the status cycle no longer passes through LegacyOnly.

### S14: Remove V21／Q7／Link controls from the settings view

- **File: symbol**: remove the EnableV21 checkbox, NotchThresholdQ7 editor, and LinkNotchThresholds toggle／help from `src/FreeformHelper.UI/Views/SettingsSections/SettingsStep5SectionView.axaml`; arrange the remaining V22 controls using the existing Grid／settingsFieldTile／tokens and simplify dual-version explanations.
- **Prerequisites**: S02, S07, P2, C-T; this slice only removes visual bindings, retaining backing properties for now so this step can build independently. If P2 has not yet handled Q7 quantization of V22 percent edits, do not delete Link／Q7 consumers or assume the remaining editor is already independent.
- **Acceptance**: B, C-T, E, U; effective values, Save／Discard, and downstream invalidation for V22 percent／NullValue／export profile remain identical; Dev／Settings previews confirm no empty columns or clipping, with no token redesign.
- **Net deletions**: estimated **45～65 lines** (AXAML only).
- **Risk**: high; Link is a V22 behavior dependency, not merely a V21 switch. Explicit user-visible change: V21 enable, Q7, and Link controls／explanations disappear.

### S15: Finally reclaim UI backing／callbacks and remove the now-unneeded V22 threshold bridge

- **File: symbol**: delete `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.State.Notch.cs:_enableV21, _lenScale, IsLenScaleVisible, _notchThresholdQ7, _linkNotchThresholds` and internal-legacy visibility state with no consumers; delete the corresponding draft state, constructor copy, and `OnEnableV21Changed, OnNotchThresholdQ7Changed, OnLinkNotchThresholdsChanged` in `SettingsWindowViewModel.cs`, removing only the now-unneeded cross-version synchronization from `OnNotchThresholdPercentChanged`; simplify same-named synchronization callbacks in `FreeformHelperViewModel.Settings.NotchPreview.cs`; delete `FreeformHelperViewModel.Settings.PropertyCallbacks.Step5.cs:OnEnableV21Changed`, corresponding Changing hooks in `FreeformHelperViewModel.UndoHooks.cs`, corresponding entries in `FreeformHelperViewModel.Settings.DirtyTracking.cs:DirtySettingNames`, and corresponding draft compare／apply in `FreeformHelperViewModel.SettingsWindow.cs:ApplySettingsWindowDraft`; reclaim `FreeformHelperViewModel.Persistence.NotchExport.Helpers.cs:ApplyNotchExportVersionSelection` and related calls in `FreeformHelperViewModel.Persistence.cs:BuildNotchTableForExportAsync` only after P1 has removed the persist-on-save version choice. Simplify `src/FreeformHelper.Application/Services/NotchTableGenerator.Generation.Thresholds.cs:ResolveEffectiveV22ThresholdPercent` to the original percent clamp only after P2 is delivered; remove log arguments／labels for retired fields from `FreeformHelperViewModel.NotchTableGeneration.cs:GenerateCurrentNotchTableAsync`.
- **Prerequisites**: S01, S02, S03, S07, S12, S14, P1, P2, C-T. A separate effort must first remove bridging references to retired fields from `FreeformHelperViewModel.Settings.Sync.cs`, `FreeformHelperViewModel.UiSnapshot.cs`, `CreateExportSettingsSnapshot`, and Application settings／validation／schema; this plan does not schedule those persistence changes. P1 must also first remove references to retired fields from the persist-on-save version choice in FreeformHelperViewModel.Persistence.NotchExport.Helpers.cs:ApplyNotchExportVersionSelection／FreeformHelperViewModel.Persistence.cs:BuildNotchTableForExportAsync; S15 does not design its save behavior. If Detail Q7 consumers have not been removed by a separate effort, retain the related threshold members; this slice must not claim full reclamation.
- **Acceptance**: B, C-T, C-G, C-R, C-K, E, U; the effective generation threshold remains exact, without treating the rounded UI percent as the old linked threshold; V22 settings draft Save／Discard／undo and the existing final-only／full invalidation policies remain identical. Do not change app-settings deferred flush, Ctrl+S／toast, selection clear, or fit／zoom reset.
- **Net deletions**: estimated **100～150 lines**; exclude Settings.Sync／snapshot／schema／enum deletions in the separate effort, and do not double-count the Application threshold helper with S07.
- **Risk**: high; generated observable properties, partial callbacks, nameof, XAML, and undo are all consumers. If P2 cannot prove that threshold semantics for the same editing action are retained, keep this code rather than bypassing the gate with a new quantization policy or defaults.

## File groups that can run in parallel and sequential boundaries

“Can run in parallel” refers to independent slices after separate authorization in the future; no removal was delegated or executed in this work. Each branch first builds／tests after its own slice, then runs B and the corresponding exact gates upon integration. Retain shared production type declarations until all callers are removed; if the test brief changes the same test file, integrate it sequentially in a separate step.

| Dependencies already satisfied | Parallel slices | Why the files do not overlap／integration gates |
|---|---|---|
| P0／P3, with characterization available for each | S01 and S03 (S03 also needs P1) | RuntimeQueryUseCase partials and Application generator partials do not overlap; run C-R／C-G on integration. |
| S01／P1 | S02 and S03 | Core／file-type metadata and the generator do not overlap. |
| S03 | S04, S05, S07 (S07 also needs S02／P1) | The two algorithm files and the generator Generation／V22／Thresholds partials do not overlap; retain legacy request／helpers until S06. |
| S02／S07 | S08, S09, S12, S13 | The four production file groups—Application exporter, Application simulation＋evaluator, UI generation＋cache, and UI selection＋view—do not overlap; run C-E, C-S, C-K, and C-U respectively. S08 actually only needs S02; waiting for S07 aligns the integration frontier. |
| S04／S05, plus each slice's own dependencies | S06 can run in parallel with S08／S09／S12／S13 | S06 only deletes the three NotchAlgorithms support files; there are no final projection／cache consumers. |
| S08／S09; other respective prerequisites are met | S10 can run in parallel with S12／S13／S14; S14 also needs P2 | S10 only deletes the V21 projector; S14 only changes SettingsStep5 AXAML and does not overlap the export-selection view. |

Must not run in parallel: S03 and S07 share Generation／Thresholds; S09→S10 because the evaluator still uses projector models; S10→S11 because the projector still uses the codec; S14→S15 because of the order in which visual bindings／VM consumers are removed; S12／S07 and S15 share VM generation／threshold files respectively. If S01 has not yet removed QueryStatus.EnableV21, S15 cannot delete the backing property. Bridging references to retired fields in the persist-on-save version choice are removed by P1 in a separate effort; do not insert save-behavior changes into S02／S12／S15.

The suggested dependency chains are `S01 → S02`, `S03 → (S04 || S05) → S06`, `(S02,S03) → S07 → (S09,S12,S13)`, `S02 → S08`, `(S08,S09) → S10 → S11`, and `(S02,S07,P2) → S14 → S15`. S15 also waits for P1 and the shared-file slices above; P0／P3 apply to every node.

## Removal counts and completion boundaries

Estimated production net deletions across the mutually exclusive slices above total **2,266～2,566 physical lines**. Of these, the whole-file total for S04／S05／S06／S10／S11 is **1,113 lines**, measured from the baseline; the rest are estimates. This excludes persistence／enum／tests／goldens in separate efforts and does not count compensation／allocation／NotchDisplayProjector as removable. This is not a performance, binary size, or duration commitment.

After implementation, run targeted searches for production references (C#／AXAML, nameof／bindings, and explicit strings), explaining each remaining V21／LegacyRegularAnchor reference. The `v2.1=empty` metadata in V22 C, allocation Q7, rule-engine compatibility, and untyped V22 fallback are intentionally retained; zero text matches cannot be required. Persistence boundaries such as `NotchAlgorithmVersionDisplay.cs`／settings enums／schema are delivered separately under P1; until that gate is closed, overall “complete removal” remains unfinished. Completion of this document's production slices does not replace it.

The only open items are external dependencies: P0 retirement authorization and unsupported request／CLI／status contracts, P1 handling of old settings and enum／UI bridges, P2 effective thresholds and existing editing semantics, and P3 characterization／retired-test coordination. Code can prove caller／numeric relationships, but cannot prove the owner has approved these product decisions; this document does not guess the answers.

## Verification for this planning work

None of the slices above was implemented in this work. The test project `--no-restore` build (including UI) passed with **0 warnings／0 errors**; class-filtered execution of `NotchTableGeneratorTests`, `NotchV22CompensationServiceTests`, `NotchV22TargetAllocationServiceTests`, `NotchApplySimulationServiceTests`, and `NotchDisplayProjectorTests` resulted in **101 passed／0 failed／0 skipped**. This is a health check of current characterization seams, not proof of equivalence for removal that has not yet happened, nor proof of GCC／runtime export／golden gates.

Commands used `AVALONIA_TELEMETRY_OPTOUT=1` and `GIT_CONFIG_GLOBAL=NUL`; there was no restore, network access, full test run, lint, process listing, git writes, or commit／push, no reading of `example/`, and no changes to code, tests, TODO, or the roadmap. The new document uses CRLF; the results of `git diff --check`, the non-empty document check, and the structure verifier are recorded in the execution response for this work, without leaving a separate report file in the working tree.
