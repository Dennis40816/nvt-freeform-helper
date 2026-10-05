# Runtime CLI / IPC (Phase A++)
Last updated: 2026-08-10

## Goals
- Allow external commands to query the "running FreeformHelper UI" and perform common workflow operations.
- Provide a reproducible debug interface for AI / automation without relying on manual UI clicks.

## Implemented commands (Phase A++)
- `freeformhelper.exe query help`
  - Returns: all supported commands, arguments, and examples.
- `freeformhelper.exe query status`
  - Returns: CAD/Regular counts, selection counts, workflow snapshot, and Notch preview state.
  - Added: `cache` (revision / hit-miss / hit-rate for the Step3 compensation cache + runtime notch query cache + Step5 export generation cache).
- `freeformhelper.exe query selection`
  - Returns: selected CAD ids, selected regular indices, and selected regular ids.
  - Added: `timings` (summary / inspector / notchPreview / total, from the latest selection pipeline).
- `freeformhelper.exe query terminal [--tail N]`
  - Returns: console log level, total line count, and the last N lines (default 120).
- `freeformhelper.exe query terminal-links [--tail N] [--limit M]`
  - Returns: clickable links parsed from the terminal (line number / offset / length / target / isUrl).
  - Purpose: verify whether the link parser matches the correct ranges without entering the UI.
- `freeformhelper.exe query pad --cad-id <id>`
  - Returns: geometry, diff/ic, matched regular, and Notch 2.2 ratios for the specified CAD pad.
  - Added: `snapshot` (from the same source as Pad Inspector) and `ruleTrace`.
- `freeformhelper.exe query pad --regular-id <id>`
  - Returns: geometry, diff/ic/freeform, and matched CAD for the specified Regular pad.
  - Added: `snapshot` (from the same source as Pad Inspector) and `ruleTrace`.
- `freeformhelper.exe query notch --cad-id <id> [--limit N] [--target-limit T] [--polygon-limit M]`
  - Returns: Notch 2.2 ratios, a To Full debug breakdown for each regular (overlap/source/blocker/reachable),
    and seed/final polygon bounds (with an optional limit).
  - Added: `stage3Allocation` (a comparison table of `cadArea/stage3Area` and `area/ratio` for each target diff).
  - Added: revision-based cache (key=`step3Revision + cadId`); repeated queries at the same revision do not recompute compensation.
- `freeformhelper.exe query multi-owner --cad-id <id> [--limit N] [--overlap-percent P]`
  - Returns: `GATE_MULTI_OWNER`, a list of `owner>1` regulars, and owner CAD ids for each cell.
  - `--overlap-percent` can override the strict overlap threshold (affects only this query, making it easier to compare 0.1% vs 1%).
- `freeformhelper.exe query notch-stage --cad-id <id> [--polygon-limit M]`
  - Returns: polygon data for the Notch 2.2 Stage overlay (stage1 seed / stage2 candidate / stage3 final),
    allowing stage-by-stage checks directly against the AA view.
- `freeformhelper.exe query notch-validation --regular-id <id>`
  - Returns: regular-centric rows from Step 5 Validation (direct / incoming / outgoing).
  - Each row uses typed-first content (`NotchTableRow.V22Node`), falling back to decoding `Values[]` if the typed payload is missing.
  - Shares the same trace mapper (`NotchValidationTraceService`) with the Step5 UI to avoid diverging UI/CLI rules.
- `freeformhelper.exe query load-project --path <project.json>`
  - Loads a project in the running UI instance (including timing in the response).
  - Timing adds `stepReplay` to show whether automatic Step1/Step2 replay occurred after loading and how long it took.
- `freeformhelper.exe query run-step --step <1|2|3|4>`
  - Triggers Step1 Match / Step2 Freeform detect / Step3 Notch preview refresh / Step4 Index diagnostics.
  - The implementation now uses the VM's single step execution entry point (shared with the UI buttons) to avoid diverging CLI/UI behavior.
- `freeformhelper.exe query clear-step --step <1|2|3|4|5>`
  - Clears the specified step's results (also clears downstream results).
  - The implementation now uses the VM's single step clearing entry point (shared with the UI clear icons).
- `freeformhelper.exe query select-cad --cad-id <id>` or `--cad-ids a,b,c`
  - Programmatically selects CAD pads (updates the view's selection/inspector context).
- `freeformhelper.exe query select-regular --regular-id <id>` / `--regular-ids ...`
  - Programmatically selects Regular pads (also supports `--regular-index` / `--regular-indices`).
- `freeformhelper.exe query clear-selection`
  - Clears the current selection.
- `freeformhelper.exe query set-tofull --enable <true|false>`
  - Toggles the Notch 2.2 To Full switch (shared with the UI setting).
- `freeformhelper.exe query export-notch --format <csv|c-v21|c-v22> --path <outputPath>`
  - `csv` means a CSV review artifact, not an FW direct-import contract; `.c` is the format for direct FW import.
  - Runs Step 5 export non-interactively (writes the file directly, without a save dialog / row selection window).
  - Internally still uses the existing Step 5 generation workflow and cache, and returns an error if prerequisites are not met.
  - Added: `elapsedMs` (the actual export duration inside the UI).
- `freeformhelper.exe query simulation [--regular-id <id>]`
  - Returns the workspace state of the current `Simulation` workspace page.
  - If `--regular-id` is specified, also returns the regular pad's `before/after/delta`, active-surface state, and notch impact summary.
  - `workspace.safety.hasSimulationSafetyViolations`, `simulationSafetyViolationCount`, and `regular.isEmsSafetyRisk` when `--regular-id` is specified are all projected from the same current `SimulationSafetyAuditResult` and Application EMS after-cap predicate (`afterValue > afterCap + 1e-9`); they must be consistent within the same snapshot. Field names, payload schema, and command arguments remain unchanged.

## Command-line arguments
- Common arguments:
  - `--timeout-ms <ms>`: IPC connection timeout (default 1500, range 1~120000).
  - `--json-pretty` / `--json-compact`: output format (default pretty).
- `query terminal`:
  - `--tail <N>`: returns the last N lines (range 1~5000).
- `query terminal-links`:
  - `--tail <N>`: scans the last N lines (range 1~5000).
  - `--limit <M>`: returns at most M links (range 1~2000, default 200).
- `query pad`:
  - Choose one: `--cad-id <id>` or `--regular-id <id>`.
- `query notch`:
  - `--cad-id <id>`: required.
  - `--limit <N>`: number of regular debug entries to return (default 200, range 1~5000).
  - `--target-limit <T>`: number of stage3 target diff entries to return (default 120, range 1~5000).
  - `--polygon-limit <M>`: number of seed/final polygon bounds entries to return (default 64, range 1~2000).
- `query multi-owner`:
  - `--cad-id <id>`: required.
  - `--limit <N>`: number of regular entries to return (default 200, range 1~5000).
  - `--overlap-percent <P>`: optional, range 0~100, overrides the strict owner overlap threshold (%).
- `query notch-stage`:
  - `--cad-id <id>`: required.
  - `--polygon-limit <M>`: maximum number of polygons to return per stage (default 64, range 1~2000).
- `query notch-validation`:
  - `--regular-id <id>`: required.
- `query load-project`:
  - `--path <project.json>`: required.
- `query run-step` / `query clear-step`:
  - `--step <N>`: required.
- `query select-cad`:
  - `--cad-id <id>` or `--cad-ids <a,b,c>`.
- `query select-regular`:
  - `--regular-id <id>` / `--regular-ids <a,b,c>` / `--regular-index <idx>` / `--regular-indices <a,b,c>`.
- `query set-tofull`:
  - `--enable true|false|1|0|on|off`.
- `query export-notch`:
  - `--format csv|c-v21|c-v22`: export format (required); `csv` = CSV review artifact, `c-v21/c-v22` = FW C contract.
  - `--path <outputPath>`: output file path (required, supports relative/absolute paths).
- `query simulation`:
  - `--regular-id <id>`: optional, queries the specified regular pad in the current Simulation workspace.

## AI quick-operation recommendations (practical)
1. First confirm that the UI instance is running (otherwise `INSTANCE_NOT_RUNNING` is returned).
2. Common complete workflow (recommended script):
   - `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/runtime/runtime-quick.ps1 -Project <project.json> -CadId <id>`
   - The script runs these in order: `load-project` → wait-ready → `run-step 1/2/3(/4)` (automatically selects CAD before step3) → `query notch-stage` → `query notch` → `query status`.
   - Built-in transient retry (`STEP_NOT_READY` / `NOT_READY` / `IPC_*`) can reduce load/step timing conflicts.
3. Minimal manual command set (without the script):
   - `freeformhelper.exe query load-project --path <project.json>`
   - `freeformhelper.exe query run-step --step 1`
   - `freeformhelper.exe query run-step --step 2`
   - `freeformhelper.exe query terminal-links --tail 300 --limit 300`
   - `freeformhelper.exe query select-cad --cad-id <id>`
   - `freeformhelper.exe query run-step --step 3`
   - `freeformhelper.exe query run-step --step 4`
   - `freeformhelper.exe query notch-stage --cad-id <id>`
   - `freeformhelper.exe query notch --cad-id <id>`
   - `freeformhelper.exe query notch-validation --regular-id <id>`
   - `freeformhelper.exe query simulation --regular-id <id>`
   - `freeformhelper.exe query export-notch --format csv --path build/perf/notch_review.csv`
   - `freeformhelper.exe query export-notch --format c-v21 --path build/perf/notch_v2.1.c`
   - `freeformhelper.exe query export-notch --format c-v22 --path build/perf/notch_v2.2.c`
   - `freeformhelper.exe query multi-owner --cad-id <id> --overlap-percent 1.0`
4. Common pitfalls:
   - `run-step --step 3` requires a selected CAD first; otherwise it returns `STEP_NOT_READY`.
   - `run-step --step 4` requires Step1 results; if Match has not run, it returns `STEP_NOT_READY`.
   - Compare the stage1/2/3 polygons from `query notch-stage` first, then inspect the per-regular rule diagnostics from `query notch`.

## 3635 V21/V22 exact export gate

- Prerequisite: close existing `FreeformHelper.UI` instances first; the exact gate launches its own hidden UI and isolates personal DXF import/view preferences with a temporary app-general-settings path.
- Entry point: `./scripts/perf/run-3635-regression-baseline.ps1 -LaunchIsolatedUi`; add `-SkipBuild` only when the apphost executable already exists.
- lifecycle: after `load-project`, calls `run-step 1~4` in order to verify that the production workflow entry points can still rebuild the complete output state.
- production single entry: UI Step5 goes directly to `ExportNotchCommand`; Runtime Query goes through `RuntimeQueryUseCase -> ExportNotchCommand`, after which both share the generation/export/write path. The CLI does not directly call the Application exporter or establish a second production workflow entry.
- `c-v21`/`c-v22` only pin the final selection/export target and do not establish a second generation algorithm. The CLI adapter does not override the VM's enabled versions or export profile for the specified format; however, the shared Step5 command still synchronizes live UI state to in-memory project settings under the existing contract and may mark `HasUnsavedChanges`. Therefore, the four-field invariant below must not be interpreted as meaning that the entire `ProjectSettings`, status, or cache metrics remain unchanged.
- Results: generates `notch_v2.1.c` / `notch_v2.2.c`, with two comparison layers:
  1. diagnostic compare: normalizes CRLF, LF, and lone CR to LF, removes at most one terminal LF from each side, then performs an ordinal compare; does not trim spaces/tabs/comments or ignore row ordering, and reports the first-diff line/column/code point on failure.
  2. hard byte gate: actual raw bytes, SHA-256, and node macro must match the signed manifest; the final gate therefore permits no encoding, newline, or any other byte drift.
- The sole machine-readable owner of executable provenance is `example/BOE36.35/notch_export_golden_manifest.json`; it pins the project, mask, both checked-in C files, node/bytes/SHA, and actual output. See `docs/performance/regression-baseline-3635.md` for human sign-off, full values, and environment.
- Failure: actual C files remain in `OutDir`; the error message lists the first differing line/column, expected/actual code points, and actual/golden paths. The gate does not overwrite checked-in golden.
- lifecycle: rejects any existing `FreeformHelper.UI`/`dotnet ... FreeformHelper.UI` process before launch, then verifies through `status.processId` that the IPC server is the managed PID; on completion, stops only the UI process that the gate itself launched and whose executable path has been verified.
- state invariant: `status.notchExportState` pins `EnableV21`, `EnableV22`, file type, and profile; all four must be identical before and after consecutive exports.
- side effects: the query creates the output directory, writes or overwrites the specified output file, updates export status/progress/summary, and looks up/stores the generation cache; file type and selection delegates are transient adapters and must be restored in `finally`.
- `-SkipGoldenCheck` is only for custom performance experiments outside the gate; VM, IPC, or exporter slices must not use this parameter for acceptance.
- Version state/invalidation slices must also verify V22→V21 with `-ReverseCExportOrder`; both the default V21→V22 and reverse order must be exact.

## IPC protocol (v1)
- Transport: Windows Named Pipe (`freeformhelper.runtime.v1`).
- Request (single-line JSON):
  - `{ "version":"1", "command":"status", "args":{...} }`
- Response (single-line JSON):
  - Success: `{ "ok": true, "data": { ... } }`
  - Failure: `{ "ok": false, "error": { "code":"...", "message":"..." } }`

## Error codes (current)
- `INSTANCE_NOT_RUNNING`: no running UI instance is available to respond.
- `INVALID_ARGUMENTS`: invalid command-line argument format.
- `UNKNOWN_COMMAND`: unsupported query command.
- `PAD_NOT_FOUND`: the specified pad does not exist in the currently visible data.
- `NOT_READY`: complete the required prerequisites first (for example, the grid has not yet been built).
- `STEP_NOT_READY`: the specified step still does not satisfy the available state after execution (usually due to missing prerequisites).
- `STEP_EXECUTION_FAILED`: exception during step execution.
- `IPC_IO_ERROR` / `IPC_ERROR`: IPC transport or execution error.
- If UI-side command execution throws an unhandled exception, the Named Pipe server must return a single-line `IPC_ERROR` failure envelope and retain the original message; disconnection/`EMPTY_RESPONSE` must not replace the protocol response. Lifecycle cancellation still terminates the server directly and is not converted to an error response.
- `IPC_TIMEOUT`: connected to a running UI instance, but the query did not complete within the `--timeout-ms` limit.
- `IPC_REQUEST_TIMEOUT`: the client connected to the IPC server without sending a complete request, and the server terminated the connection.

## Architecture
- `Program`: determines whether this is `query` mode; queries use the IPC client, otherwise the UI starts.
- `RuntimeQueryIpcServer`: Named Pipe server inside the UI process.
- `RuntimeQueryUseCase`: maps queries to ViewModel snapshots / common workflow actions.
- `FreeformHelperViewModel.GetWorkflowStateSnapshot()`: provides workflow gate state to the CLI.
- After `R13.102b-2`, `query multi-owner` first confirms that the CAD is still in the visible collection, then obtains only one current-revision `NotchV22ResolvedResult` determined by the current setting or `--overlap-percent` override; the lightweight Inspector snapshot only supplies existing CAD response metadata, and all multi-owner evidence is projected from `resolved.Compensation.RegularDebugInfos`. CLI arguments, payload schema, ordering, limit/truncation, and error codes remain unchanged.
- After `R13.102a-2b-2`, the normal `CadAllocation` generation cache stores a compact output-request-neutral candidate batch; a matching request in the same `ExportNotchCommand` performs final projection using the current enabled versions, threshold, `NullValue`, and target coverage guard/cap, then serializes using the current profile/file type. UI and Runtime export still share the same command/write path; a batch request can carry the resolved result for the currently selected single CAD, reusing the exact instance when warm, or returning at most one when cold and promoting it to the existing per-CAD owner after currentness validation. The batch does not retain complete polygons/debug evidence for the other candidates; `LegacyRegularAnchor` retains its request-specific table cache. R13.101e also makes both the normal generator/UI compensation execute only the complete `NotchV22CompensationContext`, with the old multi-parameter API serving only as a compatibility adapter; Runtime commands, payload schema, and final-output identity remain unchanged. R13.101c-2/R13.102/R13.102a-2/R13.103 remain open under their respective exit criteria.
- The IPC schema is unchanged: `status.cache.exportGenerationCache.entryRowCount` on a batch-backed `CadAllocation` entry represents the latest projected table row count, not the candidate count or batch size; a zero-row projection can therefore show both `hasEntry=true` and `entryRowCount=0`. A warm cache hit only executes/reports phase 4 and does not fabricate phase 1～3 work.

