# Test Categories
Last updated: 2026-07-20

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
   - Console/logging infrastructure tests.
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
- `HeadlessSessionGuardTests.After_MarksTheTestAsEnded_SoLaterWorkThatThrowsDoesNotEscape` 與 `HeadlessSessionGuardTests.LeftoverWork_ThatThrowsAfterTheTestEnded_DoesNotEscapeTheDispatcher` 會刻意觸發防護；這兩個測試每次都會印出 `[HeadlessSessionGuard]` 前綴。
- `MainWindow.Close()` is cancelled while the project has no saved path, because the window asks about unsaved work. A test that needs the window closed before it ends must clear the `DataContext` first.
- A headless test that fails with "Another thread created Dispatcher.UIThread while the headless application was being set up" hit a known race with background work of an earlier test (`ROADMAP.md` S15.002, root cause six). `HeadlessDispatcherSetup` reports it; it is not fixed yet, and a rerun is expected to pass.

## Notes
- Grouping is class-name based and intended for fast iteration.
- For release verification, still run:
  - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Release`
  - `dotnet test FreeformHelper.sln -c Release`

