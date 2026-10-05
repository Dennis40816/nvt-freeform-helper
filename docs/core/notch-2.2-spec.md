# Notch 2.2 Spec（implementation-calibrated）

> Canonical reference：[`docs/reference/notch-system-reference.md`](../reference/notch-system-reference.md)
> This file retains the v2.2 math / UI / historical design background; the canonical reference takes precedence for descriptions of current implementation behavior.

Last updated: 2026-08-08

This document collects the Notch 2.2 goals, mathematical definitions, UI/output requirements, and differences from the current 2.1 flow as the baseline for subsequent development.

## 1. Goals
- Use `CadAllocation` as the main structure for Notch 2.2 (executed during Post Algo).
- Fix the semantic inconsistency of `To Regular / To Full` in 2.1.
- Change freeform compensation from "length approximation" to direct use of "area ratios".
- Preserve the 2.1 compatibility path; `NotchSettings` currently enables both V2.1 and V2.2 by default, while the V2.2-oriented canonical row remains the design source of truth.

## 2. Differences from Notch 2.1 (Key Points)
- In 2.1, `[1]` is `cad area / single anchor regular area`, and `[2]` is `cad bbox area / single anchor regular area`.
- 2.2 splits compensation into three stages:
1. `Undo NF (To Regular)`
2. `To Full`
3. `X/Y Freeform splitting and lateral parameter generation`
- The 2.2 To Full implementation has been updated to "polygon-overlap seed + boundary regular expansion":
  - The seed starts from the actual overlap of `CAD polygon ∩ regular` (not a bbox-only seed).
  - Expansion is still subject to boundary/collision constraints; this does not mean that final compensation directly uses the bbox area.

## 3. Notch 2.2 Flow (Post Algo)
1. `CAD -> Regular` allocation (retains CadAllocation)
2. `Undo NF (To Regular)`: first undo the compression/amplification effect of NF on the sensing quantity
3. `To Full`: maximize extension toward the regular outer boundary without colliding with completed pads
4. `X Freeform / Y Freeform` splits by area ratio, producing behavior corresponding to the original fields 4~9
5. Assemble the 7-field `NotchV22Node` from per-target coverage; CAD-level ToRegular/ToFull/Combined remain diagnostic only and are not used directly as the final payload.

## 4. Undo NF (To Regular) Definition
### 4.1 Physical Meaning
- NF may normalize sensors of different sizes to similar magnitudes, distorting the effect of actual geometric area on diff.
- Undo NF first reverses this distortion so that diff is more nearly proportional to geometric area.

### 4.2 Mathematical Definition (Correct Approach for Multiple Regular Pads)
- Let CAD pad `C` overlap multiple regular pads `R_i`.
- Define `A_i = Area(C ∩ R_i)`.
- Recommended Undo NF ratio:

`UndoNFRatio(C) = Σ_i (A_i / Area(R_i))`

- This "partition then sum" approach is valid because `C ∩ R_i` forms an additive partition when the regulars do not overlap.
- Do not use `Σ_i (Area(C)/Area(R_i))`; this formula counts the same CAD area repeatedly and is mathematically incorrect.

### 4.3 Differences from 2.1 To Regular
- 2.1 considers only a single anchor regular.
- 2.2 uses intersection allocation from CAD to multiple regulars, which is more stable for cases spanning cells/regulars of unequal sizes.

## 5. To Full Definition
### 5.1 Physical Meaning
- The goal is to extend boundary freeform pads as far as possible to the outermost regular boundary, simulating compensation for missing boundary pads.
- Closely packed interior pads should theoretically not expand significantly.

### 5.2 Geometry/Flow Constraints
- The extension target can only be the regular pad outer boundary (outer boundary).
- Extension must not collide with "pads that have completed To Full".
- If a neighboring pad blocks extension, partial or no extension is allowed (preserving physical feasibility).
- **Interior CAD pads (not touching regulars at the panel outer boundary) do not undergo To Full expansion**; theoretically, `ToFullRatio = 1.0`.
- Enable To Full only for "overlapping boundary regulars"; non-overlapping regulars do not participate.

### 5.3 UI and Visualization Requirements
- Provide a To Full toggle in the right Panel.
- Keep the original CAD display blue.
- Overlay To Full results with an orange outline (outline only, without competing with the blue fill).
- It is recommended to show the blocking source in Notch Inspector (which pad stopped extension).
- Split Step3 display toggles into:
  - `Show To Regular labels` (controls only To Regular label visibility)
  - `Show To Full overlay` (controls only To Full seed/candidate/final visibility)
- A single policy computes Step3 visibility rules (`ToRegularLabel / ToFullSeed / ToFullCandidate / ToFullFinal`).
- The Step3 computation gate path can be switched:
  - `To Full rule engine EN`: uses `NotchToFullRuleEngine`.
  - When disabled, use the legacy inline gate instead (rule codes remain compatible).
- When `To Full rule trace EN` is enabled, retain per-regular rule traces, with runtime query (`query notch` / `query multi-owner`) returning `ruleTrace` details.
- AA area staged preview (Step3):
  1. `Seed`（CAD polygon overlap source）
  2. `Boundary candidates` (expandable boundary regulars)
  3. `Final To Full` (expanded result)
- Stage preview display contract:
  - `Stage 1` shows only `Seed`
  - `Stage 2` shows only `Boundary candidates`
  - `Stage 3` shows only `Final To Full`
  - Do not use cumulative overlays, to avoid mistaking a previous layer for final geometry
- Seed/Candidate/Final colors and line widths are controlled by their respective tokens to avoid mixing through cross-fallback.

### 5.4 Why the Ratio Still Shows 100% When There Is "To Full"
- `ToFullRatio` is defined as:
  - `ToFullRatio = max(1.0, Stage3Area / CadArea)`。
  - Where `Stage3Area = Area(Union(CAD polygon, Stage2 applied-regular area, ToFull reachable area))`.
- Therefore, whenever the Stage3 area is nearly the same as the original CAD area (or the increase is very small), the final ratio is `1.0x` (displayed as 100%).
- Common cases:
1. Although the CAD is on the boundary, neighboring CADs block the effective expansion directions (`BLOCKED_BY_NEIGHBOR`).
2. The expansion is smaller than the display precision (for example, it appears as 100% when the UI shows only 1 or 2 decimal places).
3. The geometry is already close to a full regular (the reachable area and source area differ very little).
4. `To Full enabled` should be determined by "at least one cell actually applied (`IsToFullApplied=true`)"; a boundary candidate alone does not count as applied.

### 5.5 Boundary Blocking Rules (Using CAD 4767 / 4775 as Examples)
- Rule goals (strict):
1. To Full can target only the regular outer boundary.
2. If another CAD exists in a direction, expansion in that direction is prohibited; it must not cross/cover neighboring pads.
3. Stage2 should show only regulars "with remaining expandable directions"; a regular blocked in all four directions should not appear in Stage2.
4. Stage3 must at least contain the entire original CAD geometry plus the permitted expansion area; it must not clip away the original CAD area.
- Corresponding case requirements:
1. `CAD 4775` has `CAD 4767` on its left, so `REG 4793` should not be considered an expandable candidate.
2. The portion where `CAD 4767` occupies `REG 4793` likewise must not be considered eligible for To Full (blocked by `CAD 4775`).
3. Stage1 marks only the CAD overlap areas on "regulars that will ultimately be used by To Full"; it should not also outline boundary overlaps that will not ultimately be applied.
4. Stage3 must use `original CAD outline ∪ Stage2 expandable area` as the final outline, and the result must be a closed outer outline.

## 6. Splitting (X/Y Freeform)
- Allocate by area ratios in the X/Y directions to generate behavior corresponding to the original fields 4~9.
- Add `XY Freeform` to the next official release TODO (not implemented in this release).

## 7. Parameters and Output Format (2.2)
### 7.1 App/Diagnostic Layer (Can Be Observed Separately)
- `ToRegularRatio`（Undo NF）
- `ToFullRatio`
- `CombinedRatio = ToRegularRatio * ToFullRatio`（CAD-level diagnostic only）
- These three values can be used for UI, trace, and compatibility reports; the source-wide `CombinedRatio` must not be distributed evenly or written directly into each target's Firmware payload.

### 7.2 Per-target coverage (Current Canonical Computation)

For each `(IC, target diff)`, first compute effective coverage per regular, then group and sum:

```text
coverage(ic,diff) = Σ_regular effectiveArea(regular) / regularArea(regular)
```

- `ConservativeNoGain`: `effectiveArea = SourceArea`, namely the CAD overlap coverage in each cell.
- `CurrentGain`: only regulars with `IsToFullApplied=true` use `max(SourceArea, Stage3EffectiveArea)`; the rest still use `SourceArea`.
- `Disabled`/legacy baseline: retain source-area-dominant allocation without claiming target-regular coverage.
- eligibility: final legs contain only target groups in the anchor IC, with a non-anchor/source diff, that pass the strict threshold or have ToFull applied coverage; not every overlapping target necessarily enters C.
- rounding: first apply `Math.Round(ratio * 100)` to each target group; if the absolute value exceeds 100, split it into multiple `<=100` chunks, then assemble continuation rows.
- `CombinePercent`: the retained anchor group plus the positive target legs actually emitted; this is a final row assembly value, not the CAD-level `ToRegularRatio * ToFullRatio`.

### 7.3 2.2 final Notch Table（7 fields）

The typed payload is `NotchV22Node`:

1. `AnchorDiffIndex`
2. `CombinePercent` (`0..255`, `100` = no scaling)
3. `TargetDiffIndex1`（null sentinel = none）
4. `TargetRatioPercent1`（`-100..100` signed percent）
5. `TargetDiffIndex2`
6. `TargetRatioPercent2`
7. `Flags` (includes continuation)

- continuation rules:
  - If a single row has more than 2 targets, split it into multiple continuation rows.
  - Apply the actual `CombinePercent` on the first row; fix subsequent rows at `100` (transfer legs only).
- Export gate：
  - A final raw `CombinePercent > 255` is invalid; Export must abort and show an explicit error in the UI, without clamping first to hide the overflow.

## 8. 2.1 Compatibility Rules
- Treat `LenScale` as a 2.1 legacy parameter.
- The 2.1 leg ratio now directly outputs `UINT8 0..255` Q7 magnitude (`128=100%`, `255≈199%`); sign is carried only by the ADD/SUB type, and `LenScale` is no longer used for external quantization.
- encode uses AwayFromZero; the final projector saturates at the ABI boundary, and firmware apply uses `(INT16 source * magnitudeQ7) >> 7`.
- The 2.1 `ThresholdQ7` is a separate `0..128` admission gate and is not combined with the payload range.
- The UI no longer displays `LenScale`.
- The 2.2 main flow can use area ratios directly without depending on `LenScale`.

## 9. Current System Match / Overlap Algorithms (Implementation Notes)
### 9.1 CAD↔Regular match（`PadMatcher`）
File: `src/FreeformHelper.Application/Services/PadMatcher.cs`
- Clip candidate row/col ranges against grid edges using the CAD bbox (reducing the number of comparisons).
- Compute the `Polygon ∩ Rect` intersection area for candidate cells.
- Generate bidirectional many-to-many relationships:
  - `CadToRegular`
  - `RegularToCad`
- Also retain the best match for each regular (compatible with the existing flow).

### 9.2 DXF Overlap Detection (`DxfOverlapAnalyzer`)
File: `src/FreeformHelper.Application/Services/DxfOverlapAnalyzer.cs`
- First group duplicates by polygon signature (count same-layer/cross-layer cases separately).
- Then pair overlap candidates:
  - Small datasets: all pairs
  - Large datasets: accelerated with spatial buckets
- The narrow phase uses polygon overlap to determine "actual overlap"; touching edges/points do not count as overlap.

## 10. Default Behavior (Current Decisions)
- Default enabled outputs: `v2.1 + v2.2`; the V2.2-oriented canonical row is the design source of truth, and V2.1 is produced by the final compatibility projector.
- Default computation mode: `CadAllocation` (Notch 2.2 baseline).
- `v2.1` is the legacy fallback.

## 11. Performance Optimization Opportunities (Development Notes)
- Intersection area cache: shared by match/UndoNF/splitting to avoid recomputing the same data.
- Introduce a spatial index (grid bucket / spatial hash) for To Full collision checks.
- Incremental recomputation: recompute only the CAD set affected by setting changes.
- Avoid rerunning the entire flow when TH changes (prefer filtering at the output layer).
- Switch Inspector/visualization to local AOI rendering to reduce redraw costs in large scenes.

## 12. To Confirm
- The final rule for To Full pad processing order (diff idx / boundary distance / area priority).
- Any future addition of fields or ABI changes in 2.2 requires a separate product behavior change; the current leg contract is fixed at `INT8 -100..100` signed percent.
- The schedule for `XY Freeform` to enter an official release and its output field definitions.

## 13. Diff idx Numbering Mechanism (Step 3)
- Purpose: handle cases in actual measurement files where `Diff idx` may skip numbers, have a variable starting value, or lack `Diff 0`.
- Key principle: `Diff idx` uses **independent numbering for each IC** (IC local); numbering must not be mixed across ICs.
- Automatic numbering source: `CAD scan order`.
- Adjustable parameters:
1. `CAD Output FW Diff start`: starting number (for example, start at 1).
2. `CAD Output FW Diff anchor CAD id`: starting position for automatic numbering (-1 means the first CAD in scan-order).
3. Each CAD's `CAD Output FW Diff override`: manually specify a single-point number (gaps allowed).
- Priority (fixed): `override > auto(anchor + scan order)`, with conflicts checked only within the same IC.
- Subsequent numbering rules:
  - Overrides reserve their numbers first;
  - The remaining CADs count from `start`, automatically skipping reserved values before incrementing.
- Visualization recommendations (implemented direction):
  - Overlay can be toggled;
  - Use different outline colors for anchor and override;
  - Pad Info explicitly shows the source (Auto / Auto(anchor) / Manual override).

## 14. Phase Progress (2026-02-22)
- Phase-1 implemented:
  - The `Undo NF (To Regular)` formula is integrated into the 2.2 path.
  - `To Full` provides a right-side toggle and ratio fields.
  - To Full core rules have been updated: only boundary regulars can expand; center regulars do not expand.
  - Step3 supports AA staged preview (seed / candidate / final).
  - Notch detail can display `ToRegular / ToFull / Combined` separately.
  - The invalidation chain between Steps is implemented (changes to an earlier step can clear later results), and each step provides a clear action.
  - Step3 staged preview supports autoplay and a stage legend.
  - To Full final overlay is now the topmost visual layer and supports translucent fill to avoid being obscured by the base drawing.
  - `H5` Overlay clarity consolidation is complete (policy + UI display toggles + token consolidation).
  - `H6` Step-by-step visualization is complete: Step3 shows the current stage summary, layer active/hidden state, and autoplay summary.
  - `H7` Right Panel uses tabs: `Settings / Inspector` are displayed separately to reduce repeated field density.
  - `R4` Step5 verification panel has converged on `DIRECT / IN / OUT` grouped display (retaining `NotchValidationTraceService`).
  - `Show To Regular labels` is integrated into the quick toolbar / SettingsWindow / project snapshot / app-general white-list.
  - Runtime CLI supports `query notch` for a single CAD and uses a revision-based cache (`step3Revision + cadId`) to avoid repeated recomputation.
  - Runtime CLI adds `query notch-stage --cad-id` to retrieve stage1/2/3 overlay polygons directly for comparison with the screen.
  - The quick-view `Why` above the Work Area now uses linear link expansion (popup) to prevent long explanatory text from crowding the main view.
- Phase-2 pending:
  - To Full geometric expansion + collision constraints for completed pads + outer boundary connectivity restrictions.
  - Manage Notch/Freeform rule dependencies and automatic recomputation through a Rule pipeline (DAG + dirty propagation).
  - The pad presentation snapshot is shared by Popover, Runtime CLI, and the right Inspector; deferred Inspector still computes compensation separately, and R13.102/102a consolidates computation into a single result.
  - Layered UI presentation: the first phase is complete (Top Strip summary + Why, right Inspector details from the same source); only information density/placement adjustments remain.
  - The first version of stricter To Full boundary blocking is implemented (boundary connectivity + no expansion for multi-owner + actual Stage3 area computation); case refinements and geometry visualization adjustments remain.
  - `To Full enabled` is determined by "at least one cell with `IsToFullApplied=true`" to avoid semantic confusion between enabled/100%.

## 15. Goals Confirmed for This Round (Freeze Before Implementation)
### 15.1 Rule Pipeline (Automatic Dependency Recomputation)
- Introduce a step/rule pipeline supporting:
1. Automatically mark downstream steps dirty when upstream parameters change.
2. Automatically recompute dirty steps (cancellable, throttleable), avoiding "the toggle has changed but the result is stale".
3. An explicit step dependency graph: `Match -> Freeform -> Diff idx -> ToRegular/ToFull -> Export`.

### 15.2 Pad Inspector Shared Presentation Snapshot
- `PadInspectorSnapshot` is a shared projection for the following presentation readers:
1. Left-click Popover
2. Right detail area (information view)
3. Runtime CLI (`query pad`)
- This proves only display data consolidation, not that repository-wide compensation computation already has a single entry point. R13.102a-1/R13.102b-1 have made Step3 preview, deferred Inspector, and normal Notch Detail command share a revisioned per-CAD resolved result; the export generator and the remaining readers' per-CAD/per-IC batches are still R13.102/102a debt.
- Status:
  - Complete: Popover (single selection) and `query pad` read the same snapshot; `RuleTrace` is displayed in both Popover and CLI.
  - Complete: ViewModel adds `CurrentPadInspectorSnapshot` (for integration with the right/top information layers).
  - Complete: the right detail area binds directly to the snapshot, replacing scattered presentation field sources.

### 15.3 UI Information Layers (Avoid Duplication and Confusion)
- The right Panel focuses on "settings" and does not carry duplicate information.
- Add an information strip (Top Strip) above the Work Area to display:
1. `ICx/diffY`
2. Diff source (Strict/Override/Probable)
3. Confidence
4. `ToRegular/ToFull`
5. `Why` (expands rule trace)
- Popover retains only the minimum necessary information and navigation actions.
- Status:
  - First phase complete: Top Strip can display the selected pad's primary-key summary, source/Match, and compensation summary, and can expand Why(rule trace).
  - Complete: the right detail view binds to the same snapshot (removing duplicate information sources).

### 15.4 Diff idx Display Modes (UI Display Layer Only)
- `Strict`: show only override + strict unique match; display `-` when undetermined.
- `Hybrid`: use strict first; fill missing values with most-likely and mark them `P`.
- `MostLikely`: display the best candidate for all entries, with markers for conflicts/low confidence.
- `Export` still follows `Strict` and is unaffected by the UI display mode.

## 16. Related Specs and Status Synchronization
- Runtime CLI Phase-A is implemented (`query status/selection/terminal/pad`):
  - See: `docs/reference/runtime-cli-plan.md`
- Subsequent Notch/Freeform rule formalization and source consolidation follow section 15 of this document.
