# Regular Spatial Index Notes

## Conclusion (read first)
- The current `PadMatcher` is not a brute-force all-pairs match. For **regular grids**, it already uses an "implicit spatial index":
  - First, it uses binary search on `XEdges/YEdges` to locate the row/col range that corresponds to the CAD bbox.
  - It computes polygon-rect overlap only within the candidate cell range.
- For the `RegularGrid` scenario, this approach is usually more direct than an extra R-tree, and its maintenance cost is lower.

## Current Implementation
- File: `src/FreeformHelper.Application/Services/PadMatcher.cs`
- Main flow:
  1. `GetCandidateRange()` uses the bbox and boundary arrays to find the row/col interval.
  2. Only regular pads within the candidate interval are traversed.
  3. A bbox intersection pre-filter runs first, then `IntersectionAreaWithRect` is computed.
  4. Overlaps below the overlap floor (absolute and relative values) are ignored directly.

## Complexity View
- In theory, each CAD is not `O(totalRegular)`, but `O(logR + logC + candidateCells)`.
- In a regular grid, `candidateCells` roughly approximates the number of cells covered by the CAD bbox, which is usually far smaller than the global regular count.

## When Upgrading to R-tree / Spatial Hash Is Needed
- Regulars are no longer a regular grid (for example, a large proportion of irregular regular sources are used).
- Candidate intervals are generally too large (a single CAD often covers many cells), so `candidateCells` cost gets out of control.
- The same index must be shared across multiple geometry sets (not only the regular grid).

## Suggested Next Steps (Low Risk)
1. [x] First add telemetry (average candidate cells per Match, p95 candidate cells, number of intersection calls).
   - `PadMatchResult.Telemetry`: `candidate/bounds/polygon` counts + `avg/p95`.
   - Step1 log: `PERF PADMATCH: ...`.
   - Report script: `scripts/perf/extract-padmatch-telemetry.ps1` (generates `build/perf/padmatch-telemetry-latest.md` from `build/logs/app.log`).
2. [x] Measure with a real project (such as 36.35) whether the bottleneck is in candidate scanning.
   - Decision script: `scripts/perf/evaluate-padmatch-telemetry.ps1` (outputs `build/perf/padmatch-decision-latest.md`).
   - Default thresholds: `avgCandidate/CAD <= 120`, `p95Candidate/CAD <= 400`, `polygonIntersections <= 2,000,000`.
3. [x] Only introduce R-tree/Spatial hash when telemetry shows candidate cost is too high.
   - Current decision: by default keep `XEdges/YEdges + candidate-range` to avoid the extra index maintenance cost.
   - Only enter the R-tree/Spatial hash POC if the decision script triggers the thresholds.
