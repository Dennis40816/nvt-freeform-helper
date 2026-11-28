# Test Categories
Last updated: 2026-11-28

This repo now provides a grouped test runner for faster local feedback:

- Script: `scripts/tests/run-tests.ps1`
- Test project: `tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj`

## Groups
1. `all`
   - Full test project.
2. `workflow`
   - Workflow pipeline regression only.
3. `application`
   - Domain/application logic tests (Cad/Dxf/Freeform/Notch/Project/Sizing/Workflow).
4. `notch-golden`
   - Checked-in exact baselines: 3635 V21/V22 C export, 3635/TM8.1 snapshots, and TM8.1 acceptance matrix.
5. `notch-core`
   - `notch-golden` plus generator, exporter, V22 pipeline, simulation, projection, settings, and Runtime Query contracts.
6. `infrastructure`
   - Logging infrastructure tests (`AppLogFormattingTests`, `LoggingConfigurationTests`).
7. `ui-core`
   - UI behavior tests (canvas, viewmodels, smoke). The union of `ui-stable` and `ui-viewmodel`.
   - `ui-stable`: every `ui-core` class except `FreeformHelperViewModelTests`.
   - `ui-viewmodel`: `FreeformHelperViewModelTests` only. CI runs it as its own shard, which does not block merging while `ROADMAP.md` S15.002 is open.
8. `ui-snapshots`
   - Snapshot and layout guard tests.
9. `smoke`
   - Fast smoke gate (`HeadlessUiSmokeTests` + `WorkflowPipelineServiceTests`).
10. `uncategorized`
   - Every test class no other group names, so the groups together always cover `all`.

## Examples
```powershell
./scripts/tests/run-tests.ps1 -Group smoke
./scripts/tests/run-tests.ps1 -Group workflow
./scripts/tests/run-tests.ps1 -Group application
./scripts/tests/run-tests.ps1 -Group notch-golden
./scripts/tests/run-tests.ps1 -Group notch-core
./scripts/tests/run-tests.ps1 -Group ui-core
./scripts/tests/run-tests.ps1 -Group ui-snapshots
./scripts/tests/run-tests.ps1 -Group all -Configuration Release
```

## Headless UI tests
- Tests marked `[AvaloniaFact]` or `[AvaloniaTheory]` share one headless session, which builds a new application and dispatcher for every test.
- A window still open when such a test ends breaks the session for the tests after it (`ROADMAP.md` S15.002, root cause five). `HeadlessSessionGuardAttribute` (assembly level, `tests/FreeformHelper.Tests/UI/TestHost`) therefore closes the windows a test left open and runs the queued dispatcher work after every headless test; a window that refuses to close fails that test.
- The guard only sees windows that were shown. A test that builds a window and only measures it must call `HeadlessSessionGuardAttribute.CloseAtTestEnd(window)` right after creating it.
- When the session does break, every later headless test of that run fails at its start with "The headless session still has the synchronization context of an earlier test". Only the first of those failures matters; its message names the headless test that finished just before.
- Work that throws on a test's dispatcher after the test has ended, or while the session tears the application down, does not fail anything: the guard writes it to the error stream with the prefix `[HeadlessSessionGuard]`. Search the test output for that prefix when looking for work that outlives its test.
- `HeadlessSessionGuardTests.After_MarksTheTestAsEnded_SoLaterWorkThatThrowsDoesNotEscape` and `HeadlessSessionGuardTests.LeftoverWork_ThatThrowsAfterTheTestEnded_DoesNotEscapeTheDispatcher` trigger the guard on purpose. These two tests print the `[HeadlessSessionGuard]` prefix on every run.
- `MainWindow.Close()` is cancelled while the project has no saved path, because the window asks about unsaved work. A test that needs the window closed before it ends must clear the `DataContext` first.
- A headless test that fails with "Another thread created Dispatcher.UIThread while the headless application was being set up" hit a known race with background work of an earlier test (`ROADMAP.md` S15.002, root cause six). `HeadlessDispatcherSetup` reports it; it is not fixed yet, and a rerun is expected to pass.

## Waiting in asynchronous tests
Follow rules 2 and 3 of the Core [testing rules](https://github.com/Dennis40816/nvt_fw_core/blob/main/docs/core/testing.md): do not wait for real time, and give every wait a limit. In this repository:

- Wait for something that completes only after the work under test is done, such as the command task or a window's `Closed` event.
- One `HeadlessUiPump.YieldAsync()` is not such a signal. It waits only for the jobs already queued at the default dispatcher priority or higher. Lower priority jobs can remain, and a continuation from a thread pool thread can be posted later.
- When there is no task to await, use `HeadlessUiPump.WaitUntilAsync(condition, message)`. It runs the queue until the condition holds, and it fails after 10 seconds.
- `TestContext.Current.CancellationToken` is not a time limit. A wait that only has this token can hang the test.
- Release every gate before the test ends, then wait for the code behind the gate to finish. If a close is still pending, the window refuses to close.
- Reaching a gate does not mean that earlier dispatcher posts have run. Progress reports can still be queued. Before you assert a change notification, set a value that the change differs from.
- To prove a fix for an order-dependent failure, force the failing order with a temporary product edit. The old test must fail and the new test must pass. Do not commit the edit, and state the result in the pull request.

## Notes
- Grouping is class-name based and intended for fast iteration.
- For release verification, still run:
  - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Release`
  - `dotnet test FreeformHelper.sln -c Release`

