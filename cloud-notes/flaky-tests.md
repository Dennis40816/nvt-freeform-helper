# Flaky test report (cloud run, 2026-11-05)

## Setup
- Linux x64 container, .NET SDK 10.0.301 (installed via dotnet-install), `python scripts/fetch_core_packages.py` OK.
- `dotnet build tests/FreeformHelper.Tests -c Release` (0 errors, 351 warnings).
- Framework: xUnit v3 (3.2.2) in-process runner, run as `FreeformHelper.Tests -trx runN.trx` from `build/bin/FreeformHelper.Tests/Release/net10.0`.
- 5 normal runs + 1 run with randomized order (`FreeformHelper.Tests :12345`, seed 12345). Test collections run in parallel (xUnit default).
- No code changed.

## Summary
| Run | Total | Passed | Failed | Skipped | Time |
|---|---|---|---|---|---|
| 1 | 2988 | 2942 | 3 | 43 | 236.0s |
| 2 | 2988 | 2942 | 3 | 43 | 236.4s |
| 3 | 2988 | 2942 | 3 | 43 | 237.5s |
| 4 | 2988 | 2942 | 3 | 43 | 231.1s |
| 5 | 2988 | 2942 | 3 | 43 | 230.7s |
| random (seed 12345) | 2988 | 2942 | 3 | 43 | 231.9s |

**Real flakiness found: none.** The same 3 tests failed in all 6 runs and every other test passed in all 6 (identical outcome set). Those 3 are deterministic Linux/culture failures, listed below.

## Real flaky tests (inconsistent across runs)
None. Caveat: the random-order run used one seed only, and the tests run in a single process, so shared static state is only partly exercised. Tests with long wall-time that depend on timing (see slowest list, RuntimeQuery `DisposeAsync_*`, `ProjectLoading*`) passed 6/6 here but are the best candidates to watch on slow CI.

## Linux-only deterministic failures (fail 6/6, not flaky)

### 1. `PadInfoViewModelTests.CadPadInfo_UsesSharedNotchDisplayProjectionTexts`
- Message (all runs): `Expected: "Undo NF (To Regular): 94.10%"  Actual: "Undo NF (To Regular): 94.10 %"`
- Test: `tests/FreeformHelper.Tests/UI/ViewModels/PadInfoViewModelTests.cs:82`
- Cause: culture. `{ratio:P2}` in `src/FreeformHelper.UI/Services/NotchDisplayProjector.cs:116` uses the current culture; on this Linux container that is the invariant culture, whose percent pattern is `n %` (with space), while Windows en-US gives `n%`.
- Fix: format with an explicit culture (`ratio.ToString("P2", CultureInfo.InvariantCulture)` plus a fixed pattern, or `.ToString("0.00", Invariant) + "%"`) in the projector (also `NotchDetailViewModel.cs:80`, `RegularPadInfoViewModel.Core.cs:400-401`), or set a deterministic culture in tests. Preferred: fix product code, as UI text should not depend on the host culture.

### 2. `NotchDisplayProjectorTests.Build_FromPadInspectorSnapshot_ProducesSharedDisplayProjection`
- Same message and cause as 1. Test: `tests/FreeformHelper.Tests/UI/Services/NotchDisplayProjectorTests.cs:232`. Same fix (single source `NotchDisplayProjector.cs:116`).

### 3. `FreeformHelperViewModelTests.ExportDxfVisibleCommand_WhenDxfRegularSourceLayerSelected_IncludesRegularLayerPads`
- Message (all runs): `Assert.Contains() Failure: Sub-string not found ... Not found: "8\r\nsignal\r\n"`
- Test: `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.CommandsAndUndo.InputAndExport.cs:115` (also the `regular` assertion on the next line).
- Cause: newline. The exported DXF uses `\n` on Linux (`Environment.NewLine`), the test hardcodes `\r\n`.
- Fix: have the DXF writer emit an explicit `\r\n` (DXF files are conventionally CRLF), or make the test newline-agnostic (normalize or use `Environment.NewLine`).

## Skipped tests (43 per run, environment-only, same every run)
- 26: private `example/` submodule not checked out.
- 16: RuntimeQuery fixed pipe requires Windows pipe-existence safety check.
- 1: Windows pipe access rules require Windows.

Log noise (not a failure): `error: unknown option: --cad-load-spinner` printed by a spawned child process in each run.

## 20 slowest tests (mean of 6 runs, max in parentheses)
| # | Mean s | Max s | Test |
|---|---|---|---|
| 1 | 3.69 | 7.31 | Architecture.DebtBaselineTests.CountedDebt_MatchesBaselineAndRequiresRatchetAfterCleanup |
| 2 | 3.29 | 3.87 | Architecture.DebtBaselineKeyTests.OffenderLists_NameTheCurrentSourceLines |
| 3 | 2.75 | 3.12 | UiRenderedVisualSnapshotTests.MainWindowNavigation_PageRoundTrip_PreservesRenderedWorkspace(Dark, Simulation) |
| 4 | 2.74 | 3.06 | same (Light, Simulation) |
| 5 | 2.65 | 4.72 | NotchHolePanelCompensationTests.Table_HalfTurnRotatedPanel_MapsToTheOriginalTable(circle-center, staggered, caps: False) |
| 6 | 2.47 | 2.81 | UiRenderedVisualSnapshotTests...PageRoundTrip(Light, Coordinate) |
| 7 | 2.38 | 2.68 | same (Dark, Coordinate) |
| 8 | 2.32 | 3.03 | UiLayoutGuardTests.HoverAffordances_KeepContrastTooltipsAndInputBorderScope |
| 9 | 2.23 | 2.40 | UiRenderedVisualSnapshotTests...PageRoundTrip(Dark, Dev) |
| 10 | 2.14 | 2.28 | same (Light, Dev) |
| 11 | 2.13 | 2.44 | same (Dark, HowToUse) |
| 12 | 2.10 | 2.85 | ProjectLoadingInputTests.ProjectLoad_BlocksEditingAtFrameAwaitAndRestoresItAfterCompletion |
| 13 | 2.09 | 2.49 | UiRenderedVisualSnapshotTests...PageRoundTrip(Light, HowToUse) |
| 14 | 1.91 | 2.51 | MainWindowNavigationInputTests.ClickShellTab_OtherPage_ChangesSelectionAndContentOnce(Workspace→HowToUse) |
| 15 | 1.85 | 2.34 | same (Coordinate→Workspace) |
| 16 | 1.62 | 1.92 | same (Simulation→Workspace) |
| 17 | 1.60 | 1.88 | same (Workspace→Simulation) |
| 18 | 1.57 | 1.70 | same (Simulation→Coordinate) |
| 19 | 1.51 | 1.51 | RuntimeQueryCoreBehaviorTests.DisposeAsync_WhenHandlerBlocksSynchronously_ReportsBoundAndReleasesPipe |
| 20 | 1.50 | 1.51 | RuntimeQueryCoreBehaviorTests.DisposeAsync_WhenHandlerIgnoresCancellation_ClosesPipeAndStopsWaiting |

Slowest tests are all under ~7.3s; the suite is slow through test count (~232s for 2988 tests), not outliers. Test 1 shows the highest variance (3.7s mean vs 7.3s max).
