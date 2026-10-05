# 3635 Fixed Regression Baseline (Refactoring Without Behavior Changes)

Last updated: 2026-08-08

## Purpose
- Always use `example/BOE36.35/project_3635.json` for refactoring regression.
- Complete the following in one script run:
  - Project loading and Step 1~4
  - Step 5 export (CSV review + C v2.1 + C v2.2)
  - Normalized ordinal exact gate for C v2.1/v2.2 against the checked-in golden
  - Runtime query baseline (`status` / `selection` / `notch` / `notch-validation`)
  - Selection latency sampling (prefer runtime `selection.timings`, falling back to the `Selection updated` log only when necessary)
  - Fixed budget gate (selection / inspector / export)

Historical comparison records (not the current gate):
- `docs/performance/3635-selection-export-baseline-2026-03-05.md`; that measurement did not isolate the UI and has been superseded by the signed/isolated gate in this document.

## Approved production provenance

R13.002 uses `example/BOE36.35/project_3635.json` as the Lucid 3635 authoritative input; the project SHA-256 is `A592B926AF61F64870817D5AE8A51E95143360D5504A4C9379160CDA01C88EA3`, and the embedded DXF name is `cz_36d2_6480x848_Lucid_panel_touch_block_CAD_20250729_NVTint_regular.dxf`.

The executable lock is `example/BOE36.35/notch_export_golden_manifest.json`; both the script and the Application drift test first validate the project/mask/golden SHA-256, bytes, and node count in the manifest. The generator and golden must not be changed together to obtain a false pass.

Input contract:

- selected layers: `NC.drawing`, `PGT.drawing`, `PINC.drawing`, `PLN1.drawing`, `Ref1.drawing`～`Ref5.drawing`; the raw CAD 9,843 pads become 4,838 pads after the production layer/filter path.
- hidden CAD IDs: `4764`, `4765`, `4751`, `4750`.
- import: `OnlyClosedPolylines=true`, `IncludeBlockPolylines=true`.
- regular mask: `SeeRegular.csv` enabled, SHA-256 `E858600E9467566EA4989EEF408F3D8AC63F7E6CD57F1FBF23D1DAAFB9B970B2`; 4,898 of the 4,992 regular pads are active.
- Notch: `CadAllocation`, `ConservativeNoGain`, V21 threshold Q7 `0`, V22 threshold percent `0`, linked thresholds, V21+V22 enabled, ToFull rule engine enabled, Release profile; trace disabled.

Approved outputs:

| Version | Nodes | Bytes | SHA-256 |
|---|---:|---:|---|
| V2.1 | 692 | 130,979 | `8961B8155B0571B193C7C87D8EEA75077B4EF8822828506C4661B50BA2E57488` |
| V2.2 | 548 | 84,023 | `5208068BBD8D82FC0A628693EF6035B31288EC674A25724C0C962CD58757BB47` |

The old checked-in C was produced by the raw `_cad` test-only seam: V2.1 had 640 nodes / 123,178 bytes / `D76296EF8428477A7F61A30FCFE010166B87A8185109974464F8F18E2ADEE08B`; V2.2 had 516 nodes / 79,587 bytes / `A83BC6C6814EBBC65C58D283517EA8734F7D26A05736D38086E37011A01C2BAB`.

### Calibration / approval record

| Field | Approved value |
| --- | --- |
| Golden lock commit | `3032121156dec2329e38e971327d55c7887db99f` (tree `b09ec2935a9406433bd56f0f2263f4492e91a462`, 2026-08-08) |
| Signed manifest | `example/BOE36.35/notch_export_golden_manifest.json` |
| Gate script | `scripts/perf/run-3635-regression-baseline.ps1`, SHA-256 `97534BCA03B568DB74B8FC9126B9E4B1F95A812BC8EE1D4280F02EB6269FC7D5` |
| Human approval | `Dennis40816` / project owner explicitly confirmed the Lucid 3635 authoritative input and the separate before/after zero-diff hard contracts for V2.1/V2.2 in this refactoring conversation on 2026-08-08 |
| Approval scope | Approves only the one-time provenance correction from the raw `_cad` test seam to the production layer/filter seam; does not authorize subsequent refactoring, Q7 correctness work, or documentation updates to rewrite the golden |

Approval/revalidation environment (2026-08-08): Windows `10.0.26200` x64, RID `win-x64`, PowerShell `7.6.3`, culture `zh-TW`, timezone `Taipei Standard Time`, .NET SDK `10.0.302`, MSBuild `18.6.11.33009`, target `net8.0`, GCC `15.1.0`. The repository has no `global.json`, so this record claims reproducibility only with the same source, command, and recorded environment, not byte reproducibility across SDKs/OSes.

Formal revalidation commands must run both export orders:

```powershell
./scripts/perf/run-3635-regression-baseline.ps1 `
  -LaunchIsolatedUi -EnforceBudget `
  -OutDir build/perf/3635-regression-r13005-forward

./scripts/perf/run-3635-regression-baseline.ps1 `
  -LaunchIsolatedUi -SkipBuild -ReverseCExportOrder -EnforceBudget `
  -OutDir build/perf/3635-regression-r13005-reverse
```

The golden checks, raw signed hashes, export-state invariant, and performance budget in both 2026-08-08 artifacts are PASS; forward is V21→V22 and reverse is V22→V21. The artifact schema currently does not embed the source revision, so these serve only as operational measurements; independently verifiable signed provenance still consists of the lock commit, manifest, tracked input hashes, and rerun command together. The latest production source tree `b35b9a50cb646be14db5c15cdd5533ab64d73867` keeps both C outputs exact.

Subsequent 1.3.x work allows only before/after exact results for each version; these two golden files must not be updated again on the grounds of refactoring or Q7 correctness.

## Latest profile and hotspot Classification

2026-08-08 R13.005 forward measurement: selection total p50/p95 `4/6 ms`, inspector p95 `1 ms`, notch preview p95 `3 ms`; CSV `404 ms / 107,806 bytes`, C V2.1 `90 ms / 130,979 bytes`, C V2.2 `83 ms / 84,023 bytes`. Reverse measurement: selection total p50/p95 `4/11 ms`, inspector p95 `1 ms`, notch preview p95 `3 ms`; CSV `519 ms`, C V2.1 `123 ms`, C V2.2 `85 ms`. These numbers are cache-aware Runtime Query end-to-end elapsed times, not method-level CPU attribution.

| Candidate | Current size | 3635 runtime evidence | R13 decision |
| --- | ---: | --- | --- |
| `NotchFirmwareCExporter.cs` | 993 physical / 906 nonblank | On the measured C export path, but the gate measures only the full 83～123 ms and does not isolate formatter CPU | Retain as a structural/measured-path hotspot; R13.103 will split projection/format after establishing the final projector boundary, without claiming that CPU dominance has already been proven |
| `CoordinatePlannerComputationService.cs` | 925 physical / 839 nonblank | The 3635 regression script does not call Coordinate Planner at all | Remove from the 3635 runtime-hotspot list; retain as an R13.206 structural candidate, requiring a separate Coordinate snapshot/profile before making a performance claim |

Therefore, R13.005 does not infer runtime bottlenecks from LOC or mistake `FinalizeCanonicalExportsMs` for C formatter timing. `docs/performance/3635-selection-export-baseline-2026-03-05.md` remains a historical record; this section and the artifacts above define the latest signed measurement.

## Prerequisites
- Close any existing FreeformHelper UI before running the Exact gate; the script starts its own isolated hidden instance.
- Do not add `-SkipBuild` on the first run or just after running the `-UseNoAppHost` gate; the exact gate build explicitly produces an apphost executable.

## Execution
```powershell
./scripts/perf/run-3635-regression-baseline.ps1 -LaunchIsolatedUi
```

Common parameters:
```powershell
./scripts/perf/run-3635-regression-baseline.ps1 `
  -ProjectPath example/BOE36.35/project_3635.json `
  -CadId 4767 `
  -RegularId 4978 `
  -SelectionCadIds "4767,4808,7107" `
  -SelectionCycles 8 `
  -OutDir build/perf/3635-regression-latest `
  -SkipNotchValidation `
  -LaunchIsolatedUi `
  -SkipBuild
```

Default golden:

- `example/BOE36.35/notch_export_v21_current.c`
- `example/BOE36.35/notch_export_v22_current.c`
- `example/BOE36.35/notch_export_golden_manifest.json`

The Exact gate requires `-LaunchIsolatedUi` and all existing UI instances to be closed first. The script rejects any existing UI/dotnet UI process, starts a hidden UI with an isolated app-general-settings path, and requires `status.processId` to equal the managed PID to prevent the fixed IPC pipe from connecting to another instance; on exit, it stops only the process it started whose executable path has been verified. Only custom performance experiments outside the gate may add `-SkipGoldenCheck`. To use another read-only baseline, use `-GoldenV21Path` / `-GoldenV22Path`; all output/golden paths must be mutually exclusive before export, and the script provides no option to update the golden.

Slices involving version state/invalidation must run V22→V21 once more with `-ReverseCExportOrder` in addition to the default V21→V22; both orders must be exact to prove that the preceding export does not contaminate the following one.

`-SkipBuild` may be used only when `build/bin/FreeformHelper.UI/Debug/net8.0/FreeformHelper.UI.exe` already exists; if the previous build used `UseAppHost=false`, remove `-SkipBuild` so the script rebuilds the apphost.

## Main Outputs
- `build/perf/3635-regression-latest/regression-baseline-summary.md`
- `build/perf/3635-regression-latest/regression-baseline-summary.json`
- `build/perf/3635-regression-latest/notch_table.csv`
- `build/perf/3635-regression-latest/notch_v2.1.c`
- `build/perf/3635-regression-latest/notch_v2.2.c`
- `build/perf/3635-regression-latest/runtime-export-c-v21.json`
- `build/perf/3635-regression-latest/runtime-export-c-v22.json`
- `build/perf/3635-regression-latest/runtime-status.json`
- `build/perf/3635-regression-latest/runtime-selection.json`
- `build/perf/3635-regression-latest/runtime-notch.json`
- `build/perf/3635-regression-latest/runtime-notch-validation.json`
- `build/perf/3635-regression-latest/selection-latency.json`
- `docs/performance/regression-baseline-3635.budget.json`

## Acceptance Criteria
1. All three Step 5 export files exist and have size > 0.
2. Both `goldenChecks` in `regression-baseline-summary.json` are `pass=true`; comparison only normalizes CRLF, LF, and lone CR to LF and allows at most one optional EOF newline. All other whitespace, comments, and ordering use ordinal exact compare.
3. `workflow.hasStep1Result~hasStep4Result` in `runtime-status.json` are all `true`.
4. `runtime-notch.json` and `runtime-notch-validation.json` return `ok=true` (marked `SKIPPED` if `RegularId=-1` and notch has no candidates).
5. `selection-latency.json` has `sampleCount > 0` and `totalMs/inspectorMs/notchPreviewMs` statistics.
6. `regression-baseline-summary.(md|json)` has `gate.pass=true`; add `-EnforceBudget` when running the script to also enforce the performance budget.

## Notes
- `selection.timings` provides runtime selection/inspector/notchPreview timings first; log parsing is used as a fallback only for older payloads or exceptional cases.
- To validate only the selection / inspector / export budget in this round, add `-SkipNotchValidation` to bypass the Step5 validation query.
- The Exact gate fails immediately if it detects an existing UI instance and requires it to be closed first, to avoid connecting to personal settings that are not isolated; if `-SkipGoldenCheck` experiment mode reuses a UI and IPC returns `INSTANCE_NOT_RUNNING`, confirm that the instance is running and not stuck loading.
- The script overwrites baseline files with the same names under `OutDir`.
- A golden mismatch preserves the actual C and lists the actual/golden paths, the line/column of the first ordinal diff, and the expected/actual code points in the error message; it does not write to or overwrite the checked-in golden.
- `export-notch` still delegates through `RuntimeQueryUseCase` to the existing ViewModel Step 5 export command; this gate validates only real UI/IPC results and does not add a second C generation path.
