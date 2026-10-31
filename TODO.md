# Temporary Core source follow-up

- [x] After the Core release that ships `UiEventRunner` (0.9.1), delete the CoreSource project, drop `NVT_CORE_SOURCE_CONSUMPTION`, and bump the Core package pin. Remove copy-specific integrity guards and exclusions. Refs [#64](https://github.com/Dennis40816/nvt-freeform-helper/issues/64).

# Spinner process sites without tests

- [ ] No deadline. `CadLoadSpinnerProcessHost.EnsureProcessStartedAsync`, the warmup `catch` in `QueueStateSync` and `CleanupProcess` have no test. Reaching them needs a real spinner process, and the only seam is `Process.Start` (`CadLoadSpinnerProcessHost.cs`). Moving that statement behind a delegate makes the ledger count a new banned-API fingerprint (RS0030), which it refuses. When the ledger tool stops treating a move as a new fingerprint, put `Process.Start` behind a delegate and test these sites.
- [x] The pipe server loop of `CadLoadSpinnerWindow` used a bare `catch`. It now uses `catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))`, so an out-of-memory exception in the request handling is no longer swallowed there. It now ends the loop instead (see the next item). `bareCatch` went from 1 to 0 and `catchException` is unchanged.
- [ ] No deadline. The spinner's IPC loop fails silently when the pipe cannot be created or `WaitForConnectionAsync` fails with anything other than cancellation: the `NamedPipeServerStream` constructor in `RunIpcLoopAsync` is outside every `try`, and `CadLoadSpinnerWindow` keeps the faulted task in `_ipcLoopTask` without observing it. The same silent end also happens when an out-of-memory exception escapes the request handling: the loop ends, the unobserved task faults, nothing is logged, and the spinner stops answering IPC until its process ends. Handle and log all of these in a separate behavior PR. Refs [#64](https://github.com/Dennis40816/nvt-freeform-helper/issues/64).

# Legacy console follow-up

- [x] The Core console list replaced the legacy console panel. The text buffer of `ShellViewModel.Console.cs`, its gate and the `OnLogEntriesChanged` hook are deleted. Refs [#64](https://github.com/Dennis40816/nvt-freeform-helper/issues/64).
- [x] Core 0.9.4 is pinned and the console zoom is connected. `ConsoleSession` keeps the zoom value (Core does not store it), the panel assigns it when it creates the list, and the `ZoomChanged` event saves it (range 80% to 300%). Refs [#64](https://github.com/Dennis40816/nvt-freeform-helper/issues/64).
- [ ] Refresh the stale locations in `eng/code-health/baseline.json`. Four entity locations of `FreeformHelperView` still name the deleted console partials (`FreeformHelperView.Console.cs`, `.Console.Events.cs`, `.Console.Rendering.cs` and `.ConsoleLinks.cs`), and the surviving `Logger` finding names `FreeformHelperView.Console.cs` although the field now lives in `ConsoleHost.cs`. Do it when `LowerBaseline` can refresh a location without a new fingerprint or a higher count. Refs [#64](https://github.com/Dennis40816/nvt-freeform-helper/issues/64).
- [ ] Decide whether the console zoom should persist across restarts. It lasts for the session now. A persisted value needs an app-level setting and a rule for the deferred settings flush. Refs [#64](https://github.com/Dennis40816/nvt-freeform-helper/issues/64).

# Test seams that the ledger fingerprints make expensive

- [ ] No deadline. `AppGeneralPersistenceForTests`, `NotchCacheForTests`, `WorkflowDataForTests` and `RegularVisibilityMaskForTests` (`FreeformHelperViewModel`) expose private state objects to tests. Removing them means rewriting about 12 tests to go through public commands. Renaming them does not lower any ledger number.

# Right panel text reduction (owner rule 2026-10-17: important / debug / explanation / delete)

- [x] Inspector tab: two developer-facing texts deleted, the rule trace description is a tooltip, and Snapshot details plus Rule trace sit under one Debug details fold that is closed when the inspector shows another pad (owner answers Q25 to Q27, 2026-10-31 08:54). Proposal: `nfh-pc-notes/proposal-right-panel-debug-fold.md`
- [ ] Overview tab and Step 6 quick trace: apply the four classes word by word (write the text-by-text table first). One PR per panel
- [ ] Step 1, 2, 3, 4 and 5 panels: same, one PR each. Step 3 has the largest block of debug notes (about 75 words) that should fold or become tooltips
- [ ] Snapshot details is written twice in `RightWorkflowInspectorView.axaml` (CAD and regular). Merge them into one template in a separate refactor
- [ ] Decide whether the right panel needs one global "include debug" switch, like the message center, that opens every Debug details fold. Not needed while only the Inspector has a fold
- [ ] When NFH adopts the Core version that ships `CollapsiblePanel`, move `HidePanelBlock` and the `workspaceFoldHeader` accordion to it
