# PadCanvas Selection/Draw Perf Baseline (2026-03-06)

## Purpose
- Create the first baseline for the new observation fields in `PadCanvas`. Later optimizations can be compared directly against it.
- Metric sources:
  - Existing `Selection updated` timings (summary / inspector / notchPreview / total)
  - New `canvasSelect(...)` / `draw(...)` snapshot fields

## Sample Data and Prerequisites
- Project: `example/BOE36.35/project_3635.json`
- Version: `master` (includes `PadCanvas.Perf.cs` instrumentation)
- Sampling prerequisites:
  - App log level must be `Debug` (otherwise the `Selection updated` debug line is not output)
  - Start an app instance, then load 3635 via runtime query and run multiple `select-cad/clear-selection` calls

## Sampling Commands (as actually used)
```powershell
# Start the app (using app settings with the Debug log level)
Start-Process -FilePath "build/bin/FreeformHelper.UI/Debug/net8.0/FreeformHelper.UI.exe" `
  -WorkingDirectory (Resolve-Path ".") `
  -Environment @{ FREEFORMHELPER_APP_GENERAL_SETTINGS_PATH = "$env:TEMP\freeform-app-general-a1bed0c3941b4fce9238efd6520a8fb4.json" }

# Load 3635
& "build/bin/FreeformHelper.UI/Debug/net8.0/FreeformHelper.UI.exe" query load-project `
  --path "example/BOE36.35/project_3635.json" --json-compact --timeout-ms 120000

# Sample (multiple selections)
$ids = @(4767,3777,2089,274,291)
foreach ($id in $ids) {
  & "build/bin/FreeformHelper.UI/Debug/net8.0/FreeformHelper.UI.exe" query clear-selection --json-compact --timeout-ms 60000 | Out-Null
  & "build/bin/FreeformHelper.UI/Debug/net8.0/FreeformHelper.UI.exe" query select-cad --cad-id $id --json-compact --timeout-ms 60000 | Out-Null
}
& "build/bin/FreeformHelper.UI/Debug/net8.0/FreeformHelper.UI.exe" query clear-selection --json-compact --timeout-ms 60000 | Out-Null
```

## Metric Extraction Rules
- Log line pattern (single line):
  - `Selection updated: ... | canvasSelect(rev=...,ms=...,cadCand=...,regCand=...,...) draw(rev=...,ms=...,cadCand=...,regCand=...,visCad=...,visReg=...,stepCad=...,stepReg=...)`
- Fields captured in this run:
  - `inspector ms`, `total ms`
  - `draw.ms`, `draw.cadCand`, `draw.regCand`, `draw.stepCad`, `draw.stepReg`
  - `canvasSelect.revision` (to confirm the selection instrumentation was reached)

## Baseline Results (this run)
- Source: `build/logs/app.log` (2026-03-06 22:44 local)
- Sample count: `10`

| Metric | Value |
|---|---:|
| inspector p50 / p95 (ms) | 1 / 20 |
| total p50 / p95 (ms) | 1 / 132 |
| draw p50 / p95 (ms) | 2 / 5 |
| draw cad candidates | 4838 |
| draw regular candidates | 4992 |
| draw decimation step (cad / regular) | 1 / 1 |
| canvasSelect revision max | 0 |

## Interpretation and Limitations
- The `draw` metrics can now be observed consistently (candidate counts and draw time are comparable).
- In this sample, `canvasSelect revision = 0`:
  - Reason: runtime `select-cad` is a programmatic single-point selection, not the box-select or hit-test path.
  - To build a baseline for "box-select start latency", run one round of manual box selection or use a dedicated automated input script.

## Recommendations for Future Comparison
- After each change to `PadCanvas.SelectionEngine` or `PadCanvas.VisibleDrawListBuilder`, rerun the above process at least once.
- Suggested comparison thresholds:
  - `draw p95` must not exceed baseline by more than +20%
  - `inspector p95` and `total p95` must not exceed the existing 3635 budget
