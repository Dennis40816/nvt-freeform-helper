# 3635 Selection / Export Historical Baseline (2026-03-05)

> Status: historical snapshot. The 2026-08-08 signed/isolated measurement in `docs/performance/regression-baseline-3635.md` replaces it. Do not call this file the latest baseline, and do not use it to update golden.

## Purpose
- Measure two things on the 3635 project with fixed fields: selection latency and Step5 export time.
- Keep the old 2026-03-04 non-isolated command and its values, so readers can look up the historical trend. The reproducible template and the exact gate are only in the current baseline document.

## Test Data and Prerequisites
- Project: `example/BOE36.35/project_3635.json`
- Target CAD: `4767`
- Target Regular (notch-validation): `4792`
- Historical prerequisite: start the FreeformHelper UI first (the IPC query connects to the existing instance). This setup does not meet the current managed PID/app-settings isolation gate. It is forbidden as 1.3.x acceptance.

## Reproduction Command
```powershell
./scripts/perf/run-3635-regression-baseline.ps1 `
  -ProjectPath example/BOE36.35/project_3635.json `
  -CadId 4767 `
  -RegularId 4792 `
  -SelectionCadIds "4767" `
  -SelectionCycles 2 `
  -OutDir build/perf/3635-regression-verify `
  -SkipBuild
```

## Measured Fields (Fixed)
- Selection:
  - `sampleCount`
  - `totalMs.{min,p50,p95,max,avg}`
  - `inspectorMs.{min,p50,p95,max,avg}`
  - `notchPreviewMs.{min,p50,p95,max,avg}`
- Export:
  - `runtime-export-csv.json:data.elapsedMs`
  - `runtime-export-c-v22.json:data.elapsedMs`
  - Output file size (csv/c-v22)

## Historical Measurement (Source: `build/perf/3635-regression-verify`, 2026-03-04 16:47 UTC)

### Selection latency
| Metric | Value |
|---|---:|
| sampleCount | 2 |
| totalMs p50 / p95 | 2 / 6 |
| inspectorMs p50 / p95 | 0 / 0 |
| notchPreviewMs p50 / p95 | 2 / 6 |

### Step5 export
| Metric | Value |
|---|---:|
| CSV elapsedMs | 9682 |
| C v2.2 elapsedMs | 80 |
| CSV size | 44897 bytes |
| C v2.2 size | 65820 bytes |

### Budget gate (same measurement)
- Source: `docs/performance/regression-baseline-3635.budget.json`
- Result: `pass = true`

## Artifact Paths
- `build/perf/3635-regression-verify/regression-baseline-summary.md`
- `build/perf/3635-regression-verify/selection-latency.json`
- `build/perf/3635-regression-verify/runtime-export-csv.json`
- `build/perf/3635-regression-verify/runtime-export-c-v22.json`

## Follow-up Rules
1. Do not overwrite this historical snapshot. Do not use its old output size or hash as golden.
2. Run new measurements with `run-3635-regression-baseline.ps1 -LaunchIsolatedUi -EnforceBudget`. Record the forward/reverse command, the environment, and the signed hashes in the current baseline document.
3. If the current baseline's `totalMs p95` or `CSV elapsedMs` is more than 20% worse than the latest comparable environment, add a perf investigation item to TODO. Do not use this document's non-isolated numbers as a same-environment comparison.
