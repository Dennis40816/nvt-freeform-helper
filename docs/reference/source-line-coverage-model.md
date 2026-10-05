# Source-line length coverage model (S14.012 research notes)

> Status: candidate model and hypotheses awaiting validation; this is not a validated physical contract and does not change the allocation/compensation specification.
> Inventory baseline: `PadMatcher`, `NotchV22TargetAllocationService`, and `NotchV22CompensationService*.cs` on this branch.
> Current contract entry point: [`notch-system-reference.md`](notch-system-reference.md); see [`notch-2.2-spec.md`](../core/notch-2.2-spec.md) for mathematical and historical background.

These notes retain a research question: when polygons have the same area but different distributions of covered length along actual display source/data lines, is the measured sensed quantity still the same? The following first records geometric uses confirmed by the code, then lists candidate formulas and conditions for proceeding to a prototype. The code and existing tests can confirm algorithm behavior, but cannot prove the relationship between area or line length and real electrical signals; no panel circuit diagrams, stack-up, or measurement validation were available for this study.

## 1. Terminology and data boundaries

| Term | Distinction in these notes | Data that must not be treated as equivalent |
| --- | --- | --- |
| display TFT **source/data line** | A display TFT data drive line that supplies display data voltages to selected pixels/subpixels; this candidate model studies geometric coverage of these lines. | touch Rx, FW diff, CAD pad ID; `SourceArea` in the code is not the area of this line either. |
| display TFT **gate/scan line** | A display TFT scan selection line that controls the conduction timing of the corresponding TFTs. | source/data line; neither direction may be called the source line merely because the two appear orthogonal in a diagram. |
| **touch Tx/Rx** | Touch electrodes/channels; in a mutual-capacitance context, Tx is the excitation side and Rx is the receiving side. Their actual roles in self-capacitance or integrated panels require separate confirmation. | display data/scan lines, or RegularGrid row/col; any shared structure must be confirmed by the panel design. |
| **pixel/subpixel pitch** | The repeating spacing of pixels/subpixels, with direction and measurement units specified. | Conductor width, touch electrode pitch, regular pad dimensions, or source-line pitch. Dividing pixel pitch by three does not generally yield the required line spacing. |

Actual line directions, routing offsets, effective endpoints, pixel arrangement, and the correspondence between display and touch layers must all be confirmed from panel data; these notes do not assume that source always follows Y or gate always follows X. `RegularGrid` is the basis for existing geometry/FW mapping, not a TFT circuit diagram.

## 2. Existing polygon-area uses (code facts)

### PadMatcher: overlap and matching evidence

[`PadMatcher.cs`](../../src/FreeformHelper.Application/Services/PadMatcher.cs) establishes many-to-many links using the intersection area of CAD polygons and regular bounds:

```text
overlapArea = Area(CAD polygon ∩ regular rectangle)
RegularCoverage = overlapArea / regularArea
CadCoverage = overlapArea / cadArea
```

The denominators have numerical guards; overlap must exceed the larger of the absolute floor `1e-6` and `regularArea * 0.00001` to exclude boundary contact/numerical noise. Links are sorted in descending order by `RegularCoverage`, `OverlapArea`, and `CadCoverage`; the compatible single best match for a regular and its `MatchScore` use the first entry and its `RegularCoverage`. The `MatchingSettings` parameter is currently ignored. This is geometric matching evidence, with no source-line length, pitch, or electrical weight.

### NotchV22CompensationService: SourceArea, reachability, and Stage3

The canonical entry point in [`NotchV22CompensationService.cs`](../../src/FreeformHelper.Application/Services/NotchV22CompensationService.cs) is `Compute(NotchV22CompensationContext)`. The context can hold existing allocations; only when the compatibility adapter receives no allocations does `BuildCompensationAllocations` in [`Stages.cs`](../../src/FreeformHelper.Application/Services/NotchV22CompensationService.Stages.cs) compute polygon/regular intersections and establish allocations using `overlapArea / cadArea`. This fallback must not be described as directly consuming PadMatcher links; the two also have different filtering thresholds.

| Quantity | Current calculation and use |
| --- | --- |
| `OverlapArea` | Stage A reconstructs nonnegative overlap from `allocation.Ratio * cad.Area`; entries greater than `AreaEpsilon` enter overlap diagnostics and accumulate `overlap / regularArea`. |
| `SourceArea` | Stage C records the source area for reachability only when `overlap > max(AreaEpsilon, regularArea * strictOverlapRatio)`; otherwise it records zero. The reachability helper retains the original nonnegative overlap as the source area instead of replacing it with `SourceCellCount * cellArea`. A small overlap can therefore still have `OverlapArea` but no usable `SourceArea`. |
| `ReachableArea` | When ToFull is actually applied, this is the reachable area, at least as large as overlap; when not applied, it is overlap. It represents the allowed geometric compensation region, not a measured sensing area. |
| `Stage3EffectiveArea` | Uses overlap as the inside area; when not applied, it is overlap, and when applied, it uses the reachable area. When the existing virtual-area cap is enabled, only `inside + min(reachable - inside, inside * capRatio)` is allowed into Stage3. |
| `Stage3Area` | The diagnostic basis for the entire CAD: `safeCadArea + sum(max(0, Stage3EffectiveArea - OverlapArea))`, where the sum includes only regulars with `IsToFullApplied`. It is not the sum of per-cell `Stage3EffectiveArea` values, nor can it be directly replaced by the area of preview polygons. |
| CAD-level ratios | When ToRegular is enabled, it is `sum(overlap / regularArea)`; otherwise it is `1`; `ToFullRatio = max(1, Stage3Area / safeCadArea)`; `CombinedRatio = ToRegularRatio * ToFullRatio`. These are currently retained for diagnostics and cannot replace final per-target coverage. |

[`Boundary.cs`](../../src/FreeformHelper.Application/Services/NotchV22CompensationService.Boundary.cs) builds boundary evidence from the outer grid boundary and missing or inactive neighbors; [`SpatialIndex.cs`](../../src/FreeformHelper.Application/Services/NotchV22CompensationService.SpatialIndex.cs) uses intersection area to find strict owners and blockers. [`Geometry.cs`](../../src/FreeformHelper.Application/Services/NotchV22CompensationService.Geometry.cs) generates clipped overlap/occupied union and preview polygons; [`Geometry.Reachability.cs`](../../src/FreeformHelper.Application/Services/NotchV22CompensationService.Geometry.Reachability.cs) uses the current `40 × 40` computational grid to handle ownership/reachable free cells, can expand to regular bounds when there are no foreign occupied cells, and retains the overlap lower bound.

These computational cells, cell counts, blocked/reachable area, and ToFull rule decisions are algorithmic geometry evidence. The computational cells are not pixels/subpixels or TFT or touch line spacing. `Stage3EffectiveArea` can be smaller than `ReachableArea` because of the cap; the reachable outline in the preview is not the effective area allowed into allocation.

### NotchV22TargetAllocationService: coverage and target weight

`Build` in [`NotchV22TargetAllocationService.cs`](../../src/FreeformHelper.Application/Services/NotchV22TargetAllocationService.cs) consumes compensation `RegularDebugInfos`, selects `baseWeightArea` for each cell, then groups by `(IcIndex, DiffIndex)`. When an anchor IC is specified, other ICs are excluded before calculating the allocation basis and threshold.

| Area mode | Per-cell baseWeightArea | Group Ratio |
| --- | --- | --- |
| `SourceAreaDominant` | Nonnegative `SourceArea` | group area / allocation basis area |
| `Stage3EffectiveArea` | `max(SourceArea, Stage3EffectiveArea)` when `IsToFullApplied`; otherwise SourceArea | group area / allocation basis area |
| `TargetRegularSourceCoverage` | Nonnegative `SourceArea` | `sum(baseWeightArea / regularArea)` |
| `TargetRegularStage3Coverage` | `max(SourceArea, Stage3EffectiveArea)` when `IsToFullApplied`; otherwise SourceArea | `sum(baseWeightArea / regularArea)` |

`CurrentGain` selects `TargetRegularStage3Coverage`; `ConservativeNoGain` selects `TargetRegularSourceCoverage`, whose amount is not directly increased by ToFull; other models select `SourceAreaDominant`. `Stage3EffectiveArea` is both a debug field and an area mode name; the two must not be confused.

The allocation basis is the sum of baseWeightArea for the retained groups; `PassesStrictThreshold` uses the strict condition `group area > max(AreaEpsilon, allocationBasisArea * normalizedStrictRatio)`, which is neither the PadMatcher noise floor nor the per-cell strict threshold in Stage C. Target-regular coverage divides each cell by its own regular area before summing, rather than dividing group area by the sum of all regular areas; it does not require all targets to sum to `1`.

Each group first uses `Math.Round(Ratio * 100)`. `ProjectTargetCoverage` only emits targets on the anchor IC with a non-anchor/source diff, a nonzero rounded percent, and either a passing strict threshold or ToFull applied. In target-regular modes, `RawCombinedPercent` is the retained anchor percent plus the positive target percents actually emitted; other cases/cases without an anchor retain the compatibility display. These coverage/percent values serve existing allocation, display, final projection, and guards; they are not TFT line coverage or calibrated sensed quantities. Actual row chunking and the target coverage guard still follow the current contract.

## 3. Candidate source-line formulas (not yet validated)

```text
effective signal ~= sum(source-line covered length * pitch * weight)
S_candidate(P) ~= Σ_i length(P ∩ L_i) * p_i * w_i
```

- `P`: the actual coverage polygon under study; when comparing a specific regular, use `CAD polygon ∩ regular`. Stage3 ToFull virtual expansion cannot be treated directly as real conductor coverage.
- `L_i`: the actual effective path of display source/data line i on the panel; `length(P ∩ L_i)` is the total length of the covered segments along that path, not the number of lines, bbox height, or polygon perimeter. Duplicate intersections on the same line must be deduplicated, and gaps are excluded.
- `p_i`: the transverse sampling spacing/width represented by that line, in units of length; it must be determined from the source-line arrangement, and pixel/subpixel pitch with an unidentified meaning cannot be used directly. Whether this width can approximate bent or nonuniform routing remains to be validated.
- `w_i`: a candidate weight for relative coupling/sensitivity; its source and whether it varies with position, display state, or touch channel are not yet known. These notes specify no weights, coefficients, or calibration values.

If weight is dimensionless, the right-hand side has units of length squared and is only a weighted geometric proxy; `effective signal` is a research label, and it must not be claimed to already output capacitance, raw count, or FW diff values. Predicting electrical units requires a separate conversion supported by measurements. This quantity also cannot be placed directly in existing area/ratio fields; its normalization basis and target quantity have not yet been confirmed.

## 4. Differences from the polygon-area model

The polygon-area model integrates the entire covered region; the candidate model samples along specified discrete source-line paths, then multiplies by transverse pitch and weight. The existing algorithm still treats polygon shapes, overlaps across regulars, blockers, and reachability differently; it cannot be summarized as "only considering the total area of the entire CAD."

For dense, parallel, equally spaced lines with uniform weight, `sum(length * pitch)` can approximate the polygon area integral; in this case, the candidate model may simply calculate the same quantity in another way. **Equal areas do not necessarily produce different effective signals.** The two models may produce distinguishable predictions only when factors such as discrete line positions/spacing, effective segments, and nonuniform weights make sampling meaningful.

Research comparisons should keep the overlap area of each regular and existing ToFull/blocker conditions identical, then change the polygon's position or orientation relative to the actual lines. For example, with finite line spacing, equal-area narrow strips lying on or between lines may yield different covered lengths; this illustrates geometric sampling, not an observed panel effect. If only the total CAD area is kept identical while target overlap changes, the existing model may already differ, so this cannot serve as evidence that the source-line model is better.

## 5. Physical inferences and hypotheses awaiting validation

1. **Hypothesis H1:** Some part of the target sensed quantity has identifiable coupling to the covered display source/data lines, and the line-length distribution can provide information beyond area. The existing code does not describe this electrical relationship; first identify whether the quantity to explain is touch response, baseline, or display-induced noise.
2. **Hypothesis H2:** An approximation that adds contributions from individual lines is usable within the research scope. Fringing, interlayer distance/dielectric structure, shielding, routing impedance, and timing may make the relationship nonlinear or position-dependent; these are listed only as possible influencing factors, without confirming that any factor dominates on a specific panel.
3. **Hypothesis H3:** Pitch and weight can be identified from independent data and remain valid across samples; arbitrary weights must not be fitted to existing allocation results and then claimed as successful validation.

If gate/scan lines or touch Tx/Rx are the main source of influence, H1 must be revisited; they must not be renamed source lines, nor may the contributions of all three line types be added without evidence. ToFull's virtual geometric area also does not prove that any line coupling has physically been added.

## 6. Whether to proceed to a prototype

**Current assessment: retain this as a research hypothesis; the existing code and geometry tests alone do not yet provide sufficient support for proceeding to a prototype.** A separate research effort is worth opening only after evidence supports the following conditions:

1. Source/data line paths, directions, pitch, offsets, and effective endpoint data are available with authorized usage and can be aligned to CAD coordinates, and the correspondence to gate/scan, touch Tx/Rx, and pixels/subpixels is identified.
2. The target quantity and reproducible measurement conditions, including display/touch operating states, are clearly defined; controlled samples with equal overlap area but different line-length distributions and repeated measurements are available, with differences exceeding measurement uncertainty.
3. The line-length model can make falsifiable predictions before comparison; the data shows that residuals of the area-only model relate to line-length distribution, and the improvement is not merely due to differences in existing target overlap, ToFull, blockers, or normalization.
4. The weights have an independent basis; improvement and stability are checked using samples not used in estimation and compared with the polygon-area baseline. Training-sample fit or the existing Simulation alone is insufficient, because Simulation consumes the existing notch table and cannot independently validate physical hypotheses.
5. Practical improvement thresholds, measurement error bounds, and acceptable computational cost are agreed before research; these notes assign no numerical values to those conditions. Research comparisons can leave production allocation/compensation, Firmware C, and golden unchanged.

When line data/measurements are missing, differences can be explained by the existing model, uniform line spacing reduces the model to area, or improvement does not exceed uncertainty, the prototype should be deferred and the existing polygon-area contract retained. Even if a later prototype is supported, it constitutes only research evidence; adoption as a production model still requires a separate review and is not authorized by these notes.
