# Freeform Helper Main Algorithm Description (Most Important)
Last updated: 2026-02-23

## 0. Document Purpose and Scope
This document describes the algorithms in the Freeform Helper main flow, focusing on:
- How each step from Step1~Step5 actually calculates
- The input/output data for each step
- Main formulas and decision conditions
- Corresponding code locations (classes/methods/files)

This is the first document to read when taking over, debugging, or optimizing performance.

## 1. Core Data Models
Main data structures:
- `CadPad`: a CAD pad (polygon) after DXF import
  - `src/FreeformHelper.Domain/Pads/CadPad.cs`
- `RegularPad`: a regular grid pad (rectangular cell)
  - `src/FreeformHelper.Domain/Pads/RegularPad.cs`
- `RegularGrid`: contains `Rows/Cols`, `XEdges/YEdges`, and `Pads`
  - `src/FreeformHelper.Domain/Pads/RegularGrid.cs`
- `PadMatchResult`: Step1 CAD↔Regular overlap links
  - `src/FreeformHelper.Application/Services/PadMatchResult.cs`
- `NotchTable` / `NotchTableRow`: Step5 export tables
  - `src/FreeformHelper.Domain/Notch/NotchTable.cs`
  - `src/FreeformHelper.Domain/Notch/NotchTableRow.cs`

Geometry foundations:
- Area and centroid: `Polygon2.Area()`, `Polygon2.Centroid()`
- Polygon/rectangle intersection area: `Polygon2.IntersectionAreaWithRect(...)`
- Corresponding file: `src/FreeformHelper.Domain/Geometry/Polygon2.cs`

## 2. End-to-End Flow Overview
The UI main flow has 5 steps:
1. Step1 Geometry matching (CAD↔Regular overlap)
2. Step2 Freeform tagging (XWay/YWay/XYWay)
3. Step3 Notch 2.2 compensation and preview (ToRegular/ToFull + Stage1/2/3)
4. Step4 Mapping diagnostics (DXF idx ↔ regular one-to-one suggestions and warnings)
5. Step5 Notch export (generate and export the Notch table)

Flow orchestration strategy:
- Automatically navigate to the next step: Step1->2->3->4->5
- Invalidate and clear downstream results when upstream changes
- Corresponding file: `src/FreeformHelper.UI/Services/WorkflowPipelineService.cs`
- ViewModel entry point: `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.WorkflowSteps.cs`

## 3. Step1 Geometry Matching (Geometry Match)
Main entry points:
- UI：`MatchAsync()`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.MatchWorkflow.cs`
- Application：`PadMatcher.Match(...)`
  - `src/FreeformHelper.Application/Services/PadMatcher.cs`

### 3.1 Core Idea
Instead of global O(N*M) brute-force matching, first use `XEdges/YEdges` to narrow the candidate range, then calculate geometric intersections.

### 3.2 Candidate Range (Spatial Pruning)
For each CAD pad:
- Use `GetCandidateRange(bounds, grid)` to find the row/col range
- `FindCellIndex(...)` uses binary search to find which cell contains the boundary
- Scan only the regular cells within that rectangular region

Corresponding methods:
- `GetCandidateRange(...)`
- `FindCellIndex(...)`
- `src/FreeformHelper.Application/Services/PadMatcher.cs`

### 3.3 Overlap Calculation and Filtering
For each candidate regular:
- First check AABB: `cad.Bounds.Intersects(regular.Bounds)`
- Then calculate the actual intersection area:
  - `overlapArea = Polygon2.IntersectionAreaWithRect(cad.Polygon, regular.Bounds)`
- Filter noise:
  - `overlapFloor = max(1e-6, regular.Area * 0.00001)`
  - Retain a link only if `overlapArea > overlapFloor`

### 3.4 Link Metrics
Each link stores:
- `OverlapArea`
- `RegularCoverage = overlapArea / regularArea`
- `CadCoverage = overlapArea / cadArea`

Sort order (descending):
1. `RegularCoverage`
2. `OverlapArea`
3. `CadCoverage`

Each regular ultimately still retains a single best match (compatible with the old flow):
- `regular.MatchedCadPadId = best.CadPadId`
- `regular.MatchScore = best.RegularCoverage`

### 3.5 Step1 Output
- `CadToRegular`: one CAD maps to multiple regular links
- `RegularToCad`: one regular maps to multiple CAD links
- `Telemetry`：
  - candidate cell visits
  - bounds intersections
  - polygon intersections
  - avg / p95 candidate per CAD

These telemetry values are logged as `PERF PADMATCH` in the UI.

## 4. Step2 Freeform Tagging (Freeform Tagging)
Main entry points:
- UI：`AutoDetectFreeformsAsync()`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.MatchWorkflow.cs`
- UseCase：`FreeformTaggingUseCase.AutoDetect(...)`
  - `src/FreeformHelper.UI/Services/FreeformTaggingUseCase.cs`
- Application：`FreeformDetector.AutoTagFreeforms(...)`
  - `src/FreeformHelper.Application/Services/FreeformDetector.cs`

### 4.1 overlap-link Version (Main Path)
If Step1 has a `PadMatchResult`, the coverage distribution determines the freeform direction.

For each CAD:
- Collect its links (`CadCoverage > 1e-6`)
- Skip if links < 2
- Calculate
  - `totalCoverage = sum(link.CadCoverage)`
  - `rowCoverage[row] = sum(row links)`
  - `colCoverage[col] = sum(col links)`
  - `rowDominance = max(rowCoverage) / totalCoverage`
  - `colDominance = max(colCoverage) / totalCoverage`
  - `xSpread = 1 - colDominance`
  - `ySpread = 1 - rowDominance`

Decision:
- `max(xSpread, ySpread) < threshold` -> `None`
- `enableXY && xSpread>=threshold && ySpread>=threshold` -> `XYWay`
- Otherwise, `xSpread>=ySpread ? XWay : YWay`

### 4.2 Edge Specialization (edge specialization)
If the main decision is `None` and `EnableFreeformEdgeSpecialization` is enabled:
- Find weak cells with secondary coverage on the boundary
- Avoid false positives from tiny displacements on "both left and right + both top and bottom"
- Retain only clear single-axis/dual-axis offsets and assign `XWay/YWay/XYWay`

### 4.3 legacy fallback
If there is no `PadMatchResult`, fall back to the old logic:
- Derive `xSpread/ySpread` from the row/col span of matched regulars
- Use the same threshold to determine the direction

## 5. Step3 Notch 2.2 Compensation and Preview
Main entry points:
- Preview refresh: `RefreshNotchCanvasPreview(...)`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchDetails.cs`
- Compensation core: `NotchV22CompensationService.Compute(...)`
  - `src/FreeformHelper.Application/Services/NotchV22CompensationService.cs`
- Rule engine：`NotchToFullRuleEngine`
  - `src/FreeformHelper.Application/Services/NotchToFullRuleEngine.cs`
- Final outline: `NotchV22FinalOutlineService.Build(...)`
  - `src/FreeformHelper.Application/Services/NotchV22FinalOutlineService.cs`

### 5.1 To Regular Ratio
First find all regulars that actually overlap the CAD:
- Condition: AABB intersection + `IntersectionAreaWithRect > epsilon`

Definitions:
- `rawToRegularRatio = sum(overlapArea / regularArea)`
- If `EnableToRegular = false`, the output is fixed at `ToRegularRatio = 1.0`
- `rawToRegularRatio` is a CAD-level diagnostic value indicating how much equivalent regular area this CAD covers in the regular view.
- After beta0.9, v2.2 rows no longer multiply this CAD-level `R` into each target share as a source-wide gain; actual output uses per-target regular coverage instead.

### 5.2 To Full Gate Information
Calculate for each overlapped regular:
- `strictThreshold = max(eps, regularArea * strictOverlapRatio)`
- `hasSourceArea = overlapArea > strictThreshold`
- `ownerCadPads`: all CADs on the same regular whose overlap exceeds the strict threshold
- `hasDirectionalBlocker`: the owners include a CAD other than itself
- `isBoundaryRegular`: an effective boundary regular (outer boundary or an inactive neighbor)
- `hasEffectiveExpansion = (regularArea - overlapArea) > (regularArea * 1e-9)`

### 5.3 reachability and Rule Engine
The current Step3 path focuses on "rule gates":
- If boundary + source present + expansion present + no blocker, use `ExpandToRegularBounds(...)`
- `NotchToFullRuleEngine.Evaluate(...)` produces the rule code and whether to apply

Rule codes:
- `GATE_TOFULL_DISABLED`
- `GATE_NOT_BOUNDARY`
- `GATE_SOURCE_EMPTY`
- `GATE_MULTI_OWNER`
- `NO_EXPANSION_NEEDED`
- `EXPAND_CLEAR_PATH`

The optional rule trace records whether each gate passed.

### 5.4 Stage1/2/3 Visual Semantics
When a regular has `IsToFullApplied = true`:
- Stage1 (seed): the actual overlap polygon between the CAD and that regular
- Stage2 (candidate): the rectangular polygon of the entire regular
- Stage3 (final): the union outline of `CAD + stage2 + toFullPolygons`

UI correspondence:
- Stage layer switching and auto-play
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchDetails.cs`

### 5.5 To Full Ratio and Combined
After `RegularDebugInfos` is generated, the final numeric result of Notch 2.2 always follows a single path:
- `Stage3Area = cadArea + Σ(max(0, ReachableArea - OverlapArea))`
  - Count only regulars with `IsToFullApplied = true`
  - `cadArea` already includes the original overlap, so only the expansion delta is added
- `ToFullRatio = max(1.0, Stage3Area / cadArea)`

CAD-level diagnostic：
- `CombinedRatio = ToRegularRatio * ToFullRatio`

v2.2 row payload：
- `Current (Gain)`：`CombinePercent = Σ(stage3EffectiveAreaOnTarget / targetRegularArea)`。
- `Conservative (No Gain)`：`CombinePercent = Σ(overlapAreaOnTarget / targetRegularArea)`。
- Target leg percent also directly uses the same per-target coverage.
- This design prevents small edge regular pads from being amplified by other targets' `R`; for example, when the same CAD covers two targets at `98.6% + 58.8%`, the two target coverages should remain `98.6%` and `58.8%`, respectively.

Notes:
- Stage overlay / final outline are display projections and must not feed back as the business truth for `ToFullRatio` or `Stage3Area`.
- UI / RuntimeQuery / Inspector / export should all read the same compensation/resolved result and must not independently derive it a second time.

### 5.6 Step3 Cache (Key)
To avoid repeated recalculation:
- The cache key includes:
  - cadId
  - revision
  - cadCount
  - activeRegularHash
  - `EnableToRegular/EnableToFull/RuleEngine/RuleTrace`
  - strict overlap ratio
- Corresponding locations:
  - `BuildCadV22CompensationCached(...)`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchDetails.cs`

## 6. Step4 Mapping Diagnostics（DXF idx ↔ Regular）
Main entry points:
- UI：`AnalyzeIndexMappingAsync()`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.IndexMapping.cs`
- UseCase：`DxfRegularMappingUseCase.Analyze(...)`
  - `src/FreeformHelper.UI/Services/DxfRegularMappingUseCase.cs`
- Analyzer：`DxfRegularMappingAnalyzer.Analyze(...)`
  - `src/FreeformHelper.Application/Services/DxfRegularMappingAnalyzer.cs`

### 6.1 Candidates and Scores
Each CAD scores candidate regulars:
- Candidate range: `GetCandidateRange(...)` (including `CandidatePaddingCells`)
- Scores:
  - `iou = inter / union`
  - `distScore = 1 - clamp(distance(cadCentroid, regCentroid)/diag(reg), 0, 1)`
  - `areaRatio = min(cadArea, regArea) / max(cadArea, regArea)`
  - `score = (wIou*iou + wDist*distScore + wArea*areaRatio) / (wIou+wDist+wArea)`
  - Finally clamp to `[0,1]`

### 6.2 one-to-one Assignment
The strategy is greedy matching:
- Sort all `(cad, reg, score)` pairs by score in descending order
- Skip if either cad/reg is already occupied
- Otherwise, pair them

This is an explainable, stable, and fast strategy, not Hungarian global optimization.

### 6.3 Issue Classification
Reports and issues are generated:
- `CountMismatch`
- `UnmappedCad`
- `UnmappedRegular`
- `LowConfidence`（`assignedScore < LowConfidenceThreshold`）
- `Ambiguous`（`best-second < AmbiguousMargin`）

The top-K candidates are also retained for manual review.

### 6.4 Manual Override
`manualOverrides[cadId] = regularPadId` locks the mapping first, then runs greedy matching.

## 7. Step5 Notch Export
Main entry points:
- UI export flow: `ExportNotchAsync()`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.cs`
- Generator: `NotchTableGenerator.Generate(...)`
  - `src/FreeformHelper.Application/Services/NotchTableGenerator.cs`
- facade：`NotchExportService`
  - `src/FreeformHelper.UI/Services/NotchExportService.cs`

### 7.1 Two Computation Modes
Determined by `NotchSettings.ComputationMode`:
- `LegacyRegularAnchor`
- `CadAllocation` (default)
  - `src/FreeformHelper.Application/Settings/NotchSettings.cs`

### 7.2 CadAllocation Mode (Main Path)
Flow:
1. Calculate allocations (overlap/cadArea) for each CAD
2. Select the anchor regular (prefer freeform with a matching matchedCadId)
3. Decide whether to output a row based on the version gate + strategy `CanHandle`
4. v2.2 uses diff-centric candidate aggregation

### 7.3 gate threshold
- v2.1: `q7 = clamp(roundAwayFromZero(ratio*128), 0, 128)` requires `q7 >= ThresholdQ7`. This is an admission gate, not the `0..255` V21 firmware leg payload.
- v2.2：
  - If `LinkVersionThresholds = true`: use `ThresholdQ7 * 100 / 128`
  - Otherwise, use `ThresholdPercentV22`

### 7.4 v2.2 diff-centric Row (Current Main Output)
First calculate compensation for each CAD:
- `compensation = NotchV22CompensationService.Compute(...)`
- `toRegularPercent = round(ToRegularRatio*100)`
- `toFullPercent = round(ToFullRatio*100)` (fixed at 100 when there is no boundary candidate)
- `toRegularPercent` / `toFullPercent` remain in comment/diagnostics
- `combinePercent` comes from the per-target regular coverage sum

Then perform target allocation:
- `NotchV22TargetAllocationService.Build(...)`
- Group by `(IC, Diff)`, pass the strict threshold, and exclude the anchor diff
- Form legs (target diff + coverage percent)

The output row payload is `NotchV22Node` (7 ints):
1. AnchorDiffIndex
2. CombinePercent
3. TargetDiffIndex1
4. TargetRatioPercent1
5. TargetDiffIndex2
6. TargetRatioPercent2
7. Flags (continuation rows are marked continuation)

Type:
- `src/FreeformHelper.Domain/Notch/NotchV22Node.cs`

### 7.5 Legacy Strategies (Compatibility)
- `V21NotchAlgorithm`: traditional 9-column format
  - `src/FreeformHelper.Application/Services/NotchAlgorithms/V21NotchAlgorithm.cs`
- `V22LegacyRowStrategy`: a 9-column v2.2 compatibility row in the legacy path; not the canonical V2.2 owner
  - `src/FreeformHelper.Application/Services/NotchAlgorithms/V22LegacyRowStrategy.cs`

Removed:
- The partially supported legacy `v3.1 / V31` path was removed in beta0.7. The formal Step5 output retains only `v2.1 / v2.2`.

## 8. Workflow Invalidation and Consistency
Core rules:
- Step1 changes -> clear Step2~5
- Step2 changes -> clear Step3~5
- Step3 changes -> clear Step4~5
- Step4 changes -> clear Step5

Corresponding locations:
- `WorkflowPipelineService.GetDownstreamStepsToInvalidate(...)`
- `FreeformHelperViewModel.WorkflowSteps.cs`

This mechanism ensures that "when upstream changes, downstream does not reuse stale results".

## 9. Why Selection Is Sometimes Slow (Key Explanation)
The main cost of `Selection updated ... inspector=xxxxms` is usually the Inspector snapshot, not the canvas itself.

Current strategy:
1. Create a fast snapshot first (without expensive cold-start Notch details)
2. Start a deferred refresh after 200ms to calculate full Notch details in the background
3. Return to the UI thread to apply them after the background work completes

Corresponding locations:
- `UpdateInspectorSnapshotFromSelection(...)`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Selection.cs`
- `QueueDeferredCadInspectorSnapshotRefresh(...)`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.PadInspectorDeferred.cs`
- `BuildCadPadInspectorSnapshot(... includeExpensiveNotchDetails)`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.PadInspector.cs`

## 10. Main Class Reference Table (Quick Index)
| Flow | Core Class/Method | File |
|---|---|---|
| Step1 Match | `PadMatcher.Match` / `MatchOverlap` | `src/FreeformHelper.Application/Services/PadMatcher.cs` |
| Step1 Result | `PadMatchResult`, `PadMatchLink` | `src/FreeformHelper.Application/Services/PadMatchResult.cs` |
| Step2 Freeform | `FreeformDetector.AutoTagFreeforms` | `src/FreeformHelper.Application/Services/FreeformDetector.cs` |
| Step2 UseCase | `FreeformTaggingUseCase.AutoDetect` | `src/FreeformHelper.UI/Services/FreeformTaggingUseCase.cs` |
| Step3 Compensation | `NotchV22CompensationService.Compute` | `src/FreeformHelper.Application/Services/NotchV22CompensationService.cs` |
| Step3 Gate | `NotchToFullRuleEngine.Evaluate` | `src/FreeformHelper.Application/Services/NotchToFullRuleEngine.cs` |
| Step3 Final Outline | `NotchV22FinalOutlineService.Build` | `src/FreeformHelper.Application/Services/NotchV22FinalOutlineService.cs` |
| Step3 UI | `RefreshNotchCanvasPreview` | `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchDetails.cs` |
| Step4 Diagnostics | `DxfRegularMappingAnalyzer.Analyze` | `src/FreeformHelper.Application/Services/DxfRegularMappingAnalyzer.cs` |
| Step4 UI | `AnalyzeIndexMappingAsync` | `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.IndexMapping.cs` |
| Step5 Generation | `NotchTableGenerator.Generate` | `src/FreeformHelper.Application/Services/NotchTableGenerator.cs` |
| Step5 Export | `NotchExportService` | `src/FreeformHelper.UI/Services/NotchExportService.cs` |
| Workflow Orchestration | `WorkflowPipelineService` | `src/FreeformHelper.UI/Services/WorkflowPipelineService.cs` |

## 11. Recommended Reading Order (Code)
1. `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Commands.cs`
2. `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.MatchWorkflow.cs`
3. `src/FreeformHelper.Application/Services/PadMatcher.cs`
4. `src/FreeformHelper.Application/Services/FreeformDetector.cs`
5. `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchDetails.cs`
6. `src/FreeformHelper.Application/Services/NotchV22CompensationService.cs`
7. `src/FreeformHelper.Application/Services/DxfRegularMappingAnalyzer.cs`
8. `src/FreeformHelper.Application/Services/NotchTableGenerator.cs`
9. `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.cs`

