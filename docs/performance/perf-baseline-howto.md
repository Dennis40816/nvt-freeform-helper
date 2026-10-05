# Non-Notch Performance Baseline Usage

## Purpose

Establish a repeatable, comparable performance baseline, initially focusing on:
- Duration of major operations (`finished in N ms`)
- Log noise metrics (`Selection updated`, `Grid built`, `ViewModel initialized`)
- Exception and warning counts (`ERROR`/`WARN`)

This document covers runtime latency/logs. Production source LOC and secondary Release DLL size are a separate set of structural metrics; see `docs/performance/code-size-baseline-1.3.0.md`. Neither can replace the other, and their thresholds must not be mixed.

---

## Recommended Workflow for a Baseline Run

1. Start the tool and execute a fixed operation script (your usual workflow)  
   Example: load DXF -> rebuild grid -> run overlap/match -> export.
2. Close the tool (ensure log flushing has completed).
3. Run the baseline script:

```powershell
./scripts/perf/collect-perf-baseline.ps1
```

4. Inspect the output file:
   - `build/perf/non-notch-perf-baseline-latest.md`

---

## Startup/Load Performance (Phase 1)

New script: `scripts/perf/measure-startup-load.ps1`

Purpose:
- Automatically start the app and measure the time until "Runtime query is available"
- Optionally execute `load-project`
- Produce markdown/json for baseline comparison

Example:

```powershell
./scripts/perf/measure-startup-load.ps1 `
  -ProjectPath example/BOE36.35/project_3635.json
```

Output:
- `build/perf/startup-load-latest.md`
- `build/perf/startup-load-latest.json`

Additional notes:
- By default, `PERF STARTUP` / `PERF LOAD_PROJECT` markers are logged at the `Debug` level.
- `measure-startup-load.ps1` temporarily sets `FREEFORM_PERF_MARKERS_INFO=1` to ensure the measurement run also retains markers at the `Info` level.
- To manually measure startup without the script, set `FREEFORM_PERF_MARKERS_INFO=1` yourself before starting the app.
- If "initial grid background scheduling" is enabled, `hasGrid` in the `Startup` section may be `False` (expected).
- In that case, also use `PERF STARTUP stage=workspace.initial-grid-built` to observe when the first grid finishes.

---

## Cold/Warm Startup Batch Baseline

New script: `scripts/perf/measure-startup-load-batch.ps1`

Purpose:
- Run `measure-startup-load.ps1` repeatedly to produce multiple measurements in one batch
- Automatically aggregate cold (run1) and warm (run2+) statistics (avg/p50/p95/min/max)
- Retain the raw markdown/json for each run for traceability

Example:

```powershell
./scripts/perf/measure-startup-load-batch.ps1 `
  -Runs 5 `
  -ProjectPath example/BOE36.35/project_3635.json `
  -SkipBuild
```

Output:
- `build/perf/startup-load-batch-latest.md`
- `build/perf/startup-load-batch-latest.json`
- `build/perf/runs/startup-load-run-*.md/json`

Optional regression comparison:

```powershell
./scripts/perf/measure-startup-load-batch.ps1 `
  -Runs 5 `
  -ProjectPath example/BOE36.35/project_3635.json `
  -CompareJsonPath build/perf/startup-load-batch-prev.json `
  -RegressionThresholdPercent 15 `
  -FailOnRegression `
  -SkipBuild
```

- `CompareJsonPath`: path to the previous batch JSON
- `RegressionThresholdPercent`: regression threshold (percentage, default 15)
- `FailOnRegression`: return script failure if a regression is detected (can be integrated into CI)

---

## 3635 Fixed Regression Baseline (Step1~Step5 + exact export + Runtime Query)

New script: `scripts/perf/run-3635-regression-baseline.ps1`

Purpose:
- Run a fixed refactoring regression workflow for `project_3635.json` (load + Step1~Step4 + Step5 export)
- Automatically produce CSV / C v2.1 / C v2.2 export files
- By default, compare V21/V22 C produced by the actual UI/IPC against the checked-in golden using ordinal exact compare after normalizing CRLF/LF/lone CR to LF and allowing at most one optional EOF newline
- Also produce a runtime query baseline (`status/selection/notch/notch-validation`)
- Use runtime `selection.timings` as the primary source for selection / inspector latency statistics, falling back to parsing the `Selection updated` log when necessary
- Read `docs/performance/regression-baseline-3635.budget.json` and output fixed budget gate results

Example:

```powershell
./scripts/perf/run-3635-regression-baseline.ps1 `
  -ProjectPath example/BOE36.35/project_3635.json `
  -CadId 4767 `
  -RegularId 4978 `
  -SelectionCadIds "4767,4808,7107" `
  -SelectionCycles 8 `
  -SkipNotchValidation `
  -EnforceBudget `
  -LaunchIsolatedUi `
  -SkipBuild
```

The golden gate uses these by default:

- `example/BOE36.35/notch_export_v21_current.c`
- `example/BOE36.35/notch_export_v22_current.c`
- `example/BOE36.35/notch_export_golden_manifest.json`（executable hash/bytes/nodes/input lock）

The exact gate requires `-LaunchIsolatedUi`: the script launches a hidden UI with a temporary `FREEFORMHELPER_APP_GENERAL_SETTINGS_PATH` to prevent personal import/view settings from changing the CAD count or output. On completion, it stops only the process it launched whose path has been verified. A mismatch preserves the actual C and lists the actual/golden paths and the first difference in the error message; the script does not update the golden. `-SkipGoldenCheck` is only for custom performance experiments outside the gate.

`-SkipBuild` requires an existing apphost executable; if you just ran tests/lint with `-UseNoAppHost`, remove `-SkipBuild` so the script rebuilds with `UseAppHost=true` before launching the isolated UI.

Output (default):
- `build/perf/3635-regression-latest/regression-baseline-summary.md`
- `build/perf/3635-regression-latest/regression-baseline-summary.json`
- `build/perf/3635-regression-latest/notch_table.csv`
- `build/perf/3635-regression-latest/notch_v2.1.c`
- `build/perf/3635-regression-latest/notch_v2.2.c`
- `build/perf/3635-regression-latest/runtime-export-c-v21.json`
- `build/perf/3635-regression-latest/runtime-export-c-v22.json`
- `build/perf/3635-regression-latest/runtime-*.json`
- `build/perf/3635-regression-latest/selection-latency.json`

For detailed acceptance criteria and output descriptions, see:
- `docs/performance/regression-baseline-3635.md`

---

## Startup Synchronous Hotspot Inventory (Marker Breakdown)

New script: `scripts/perf/analyze-startup-markers.ps1`

Purpose:
- Automatically find the "latest app run" in the `PERF STARTUP` log (excluding query subprocesses)
- Produce a stage timeline (elapsed/delta)
- List top delta stages to quickly locate synchronous hotspots
- Note: if the app was not launched through `measure-startup-load.ps1`, the current log level is not Debug, and `FREEFORM_PERF_MARKERS_INFO=1` is not set, the script may not find markers for the latest run.

Example:

```powershell
./scripts/perf/analyze-startup-markers.ps1 `
  -LogPath build/logs/app.log `
  -OutPath build/perf/startup-markers-latest.md
```

Budget checking can be enabled directly (`workspace.initial-grid-built <= 1000ms`):

```powershell
./scripts/perf/analyze-startup-markers.ps1 `
  -LogPath build/logs/app.log `
  -OutPath build/perf/startup-markers-latest.md `
  -InitialGridBudgetMs 1000 `
  -FailOnBudgetViolation
```

Or use the test gate wrapper script:

```powershell
./scripts/tests/check-startup-budget.ps1 -SkipBuild -InitialGridBudgetMs 1000
```

---

## Script Parameters

```powershell
./scripts/perf/collect-perf-baseline.ps1 `
  -LogPath build/logs/app.log `
  -OutPath build/perf/non-notch-perf-baseline-latest.md `
  -OutJsonPath build/perf/non-notch-perf-baseline-latest.json `
  -CompareJsonPath build/perf/non-notch-perf-baseline-prev.json `
  -RegressionThresholdPercent 20 `
  -TopOperations 30
```

- `LogPath`: source log file
- `OutPath`: output markdown path
- `OutJsonPath`: output JSON path (for subsequent comparison/automation)
- `CompareJsonPath`: old baseline JSON to compare against (optional)
- `RegressionThresholdPercent`: regression threshold (only values exceeding it are listed in the regression table)
- `TopOperations`: output the top N operations after sorting by `p95`

---

## New Observable Fields

`collect-perf-baseline.ps1` now also summarizes:
- `PERF STARTUP` markers (application startup stages)
- `PERF LOAD_PROJECT SUMMARY` markers (elapsed time for project loading segments)

Two new sections appear in the markdown:
- `Startup Markers`
- `Load Project Markers`

---

## Quick Comparison Recommendations

1. Back up the previous JSON first:

```powershell
Copy-Item build/perf/non-notch-perf-baseline-latest.json build/perf/non-notch-perf-baseline-prev.json -Force
```

2. After completing the new workflow, rerun the baseline (with `-CompareJsonPath`).
3. Read the `Regression Check` section in the markdown directly.

---

## How to Read the Tables

- Look at `p95` first: it reflects perceived stuttering better than the average.
- Avoid drawing conclusions when `Count` is too small.
- If `ERROR`/`WARN` increases, rule out functional problems before discussing performance.
- An unusually high `Selection updated` count usually indicates excessive interaction triggers or log noise.

---

## Version Comparison Recommendations

- Run once before and after each perf/refactor change.
- Produce comparison files under `build/perf/`, then select key points to paste into a PR/issue if needed.
- If `p95` regresses by more than 20%, stop and perform root cause analysis first.

