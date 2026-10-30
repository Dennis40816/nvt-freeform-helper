# Temporary Core source follow-up

- [x] After the Core release that ships `UiEventRunner` (0.9.1), delete the CoreSource project, drop `NVT_CORE_SOURCE_CONSUMPTION`, and bump the Core package pin. Remove copy-specific integrity guards and exclusions. Refs [#64](https://github.com/Dennis40816/nvt-freeform-helper/issues/64).

# Spinner process sites without tests

- [ ] No deadline. `CadLoadSpinnerProcessHost.EnsureProcessStartedAsync`, the warmup `catch` in `QueueStateSync` and `CleanupProcess` have no test. Reaching them needs a real spinner process, and the only seam is `Process.Start` (`CadLoadSpinnerProcessHost.cs`). Moving that statement behind a delegate makes the ledger count a new banned-API fingerprint (RS0030), which it refuses. When the ledger tool stops treating a move as a new fingerprint, put `Process.Start` behind a delegate and test these sites.
- [x] The pipe server loop of `CadLoadSpinnerWindow` used a bare `catch`. It now uses `catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))`, so out-of-memory exceptions are no longer swallowed there. `bareCatch` went from 1 to 0 and `catchException` is unchanged.
- [ ] No deadline. The spinner's IPC loop fails silently when the pipe cannot be created: the `NamedPipeServerStream` constructor in `RunIpcLoopAsync` is outside every `try`, and `CadLoadSpinnerWindow` keeps the faulted task in `_ipcLoopTask` without observing it. Handle and log it in a separate behavior PR. Refs [#64](https://github.com/Dennis40816/nvt-freeform-helper/issues/64).

# Legacy console follow-up

- [x] The Core console list replaced the legacy console panel. The text buffer of `ShellViewModel.Console.cs`, its gate and the `OnLogEntriesChanged` hook are deleted. Refs [#64](https://github.com/Dennis40816/nvt-freeform-helper/issues/64).
- [x] Core 0.9.4 is pinned and the console zoom is connected. `ConsoleSession` keeps the zoom value (Core does not store it), the panel assigns it when it creates the list, and the `ZoomChanged` event saves it (range 80% to 300%). Refs [#64](https://github.com/Dennis40816/nvt-freeform-helper/issues/64).
- [ ] Refresh the stale locations in `eng/code-health/baseline.json`. Four entity locations of `FreeformHelperView` still name the deleted console partials (`FreeformHelperView.Console.cs`, `.Console.Events.cs`, `.Console.Rendering.cs` and `.ConsoleLinks.cs`), and the surviving `Logger` finding names `FreeformHelperView.Console.cs` although the field now lives in `ConsoleHost.cs`. Do it when `LowerBaseline` can refresh a location without a new fingerprint or a higher count. Refs [#64](https://github.com/Dennis40816/nvt-freeform-helper/issues/64).
- [ ] Decide whether the console zoom should persist across restarts. It lasts for the session now. A persisted value needs an app-level setting and a rule for the deferred settings flush. Refs [#64](https://github.com/Dennis40816/nvt-freeform-helper/issues/64).

# Test seams that the ledger fingerprints make expensive

- [ ] No deadline. `AppGeneralPersistenceForTests`, `NotchCacheForTests`, `WorkflowDataForTests` and `RegularVisibilityMaskForTests` (`FreeformHelperViewModel`) expose private state objects to tests. Removing them means rewriting about 12 tests to go through public commands. Renaming them does not lower any ledger number.
