# Repo Refactor Scan (2026-02-26)

## Scan Scope
- Branch: `refactor-code-reduction-pass1`
- Scanned files: `src/`, `tests/`, `docs/` (excluding `bin/obj/build`)
- Tracked files: `408`

## Quantified Results (This Round)
1. Line count hotspots (top 10)
   - `src/FreeformHelper.UI/Services/RuntimeQueryUseCase.cs`: 1493
   - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Operations.cs`: 1483
   - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.State.cs`: 1373
   - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Settings.cs`: 1352
   - `src/FreeformHelper.UI/Controls/PadCanvas.Rendering.cs`: 1083
   - `src/FreeformHelper.Application/Services/NotchV22CompensationService.cs`: 1040
   - `src/FreeformHelper.UI/Views/FreeformHelperView.axaml.cs`: 944
   - `src/FreeformHelper.UI/Views/FreeformHelperView.Console.cs`: 914
   - `src/FreeformHelper.UI/Services/ManualSizingService.cs`: 899
   - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchDetails.cs`: 868
2. Lint/analyzer (`./scripts/tests/lint.ps1 -AllFiles`)
   - Total warnings: `74`
   - `CA1822`: 72 (many stateless services / helpers can become static)
   - `CA1865`: 2 (single-character `StartsWith(string)` usage)
3. Sync/shutdown path risk
   - `src/FreeformHelper.UI/Services/RuntimeQueryIpc.cs` still calls `_runLoopTask?.Wait(...)`. This risks blocking on shutdown.
4. Documentation sync issues
   - `docs/reference/behavior-inventory.md` still has outdated descriptions (for example, `ShowHomeCommand`).

## Refactor Opportunities (by Priority)

### P0 (Do First)
1. **Selection/Inspector low-latency, second round**
   - Goal: reduce the delay when starting a marquee selection. Update the selection summary first, run inspector work in background batches, and block heavy work during drag.
   - Entry point: `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Selection.cs`
2. **Remove blocking from the IPC shutdown flow**
   - Goal: replace `_runLoopTask?.Wait(...)` with an async stop (with timeout/cancel) to keep the UI from hanging on close.
   - Entry point: `src/FreeformHelper.UI/Services/RuntimeQueryIpc.cs`
3. **Analyzer debt cleanup (clear CA1822/CA1865 first)**
   - Goal: mark stateless members as `static`, fix single-character `StartsWith` usage, and reduce warning noise.
   - Entry points: `src/FreeformHelper.UI/Services/*.cs`, `src/FreeformHelper.Application/**/*.cs`

### P1 (Process/Quality)
1. **Extend the lint gate to the solution level**
   - Lint currently builds only `FreeformHelper.UI.csproj`. Add a full-solution analyzer gate mode so cross-project quality debt does not build up.
   - Entry points: `scripts/tests/lint.ps1`, `scripts/tests/run-refactor-gate.ps1`
2. **Make the test naming rule a policy**
   - `CA1707` (underscore naming) is common in tests. Choose one of two options:
     - A. Rename gradually
     - B. Adjust the rule only in the tests project (keep readability)

### P2 (Architecture Maintainability)
1. **Split very large files further**
   - `RuntimeQueryUseCase`, `FreeformHelperViewModel.Settings/State/Operations`, and `PadCanvas.Rendering` are still very large.
   - Split them into modules by command domain / setting domain / render layer, and keep the single entry point.
2. **Document the Notch/Inspector recalculation policy**
   - Complete a decision table covering when to hit the cache, when to force a recalculation, and when to update only the overlay. It should be shared by UI and CLI.

### UI Display (Ongoing Optimization)
1. **Text overflow guard**
   - Add a layout guard: key information columns (Settings, Inspector, Notch export rows) must wrap and must not overflow their container.
2. **Tokenize the emphasis levels**
   - Consolidate the colors and font weights for "result / cause / secondary info" into tokens, so block visual rules do not drift.

## Verification and Thresholds
- Build: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
- Lint: `./scripts/tests/lint.ps1` (use `-AllFiles` if needed)
- Regression: `scripts/tests/run-tests.ps1 -Group application`
- Regression: `scripts/tests/run-tests.ps1 -Group ui-core`
- Regression: `scripts/tests/run-tests.ps1 -Group smoke`
