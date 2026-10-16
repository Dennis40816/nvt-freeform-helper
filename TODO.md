# Temporary Core source follow-up

- [x] After the Core release that ships `UiEventRunner` (0.9.1), delete the CoreSource project, drop `NVT_CORE_SOURCE_CONSUMPTION`, and bump the Core package pin. Remove copy-specific integrity guards and exclusions. Refs [#64](https://github.com/Dennis40816/nvt-freeform-helper/issues/64).

# Spinner process sites without tests

- [ ] No deadline. `CadLoadSpinnerProcessHost.EnsureProcessStartedAsync`, the warmup `catch` in `QueueStateSync` and `CleanupProcess` have no test. Reaching them needs a real spinner process, and the only seam is `Process.Start` (`CadLoadSpinnerProcessHost.cs`). Moving that statement behind a delegate makes the ledger count a new banned-API fingerprint (RS0030), which it refuses. When the ledger tool stops treating a move as a new fingerprint, put `Process.Start` behind a delegate and test these sites.
- [ ] No deadline. The pipe server loop of `CadLoadSpinnerWindow` keeps its bare `catch`. Changing it to `catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))` raises `catchException` from 25 to 26, because a bare catch does not count there and the new form does. The ledger only lowers. Change it when the ledger tool treats bare to this form as no increase.

# Legacy console follow-up

- [ ] When the Core console list replaces the legacy console panel, delete the text buffer of `ShellViewModel.Console.cs` with its gate (`_consoleTextGate`) and the `OnLogEntriesChanged` hook. Until then the gate keeps log notifications from other threads away from the buffer.

# Test seams that the ledger fingerprints make expensive

- [ ] No deadline. `FreeformHelperView.TryOpenDirectory` and `TryOpenFileInExplorer` (`FreeformHelperView.ConsoleLinks.cs`) have no test. Each starts `explorer.exe`, so a test would open a window. Putting `Process.Start` behind a delegate means moving its statement, and the ledger treats a moved banned-API statement (RS0030) as a new fingerprint, which it refuses. Their named `catch` list is the same as the one of `TryOpenWithShell`, which has tests with real failing inputs. When the ledger tool stops treating a move as a new fingerprint, move the three `Process.Start` calls behind a delegate and test both sites.
- [ ] No deadline. `AppGeneralPersistenceForTests`, `NotchCacheForTests`, `WorkflowDataForTests` and `RegularVisibilityMaskForTests` (`FreeformHelperViewModel`) expose private state objects to tests. Removing them means rewriting about 12 tests to go through public commands. Renaming them does not lower any ledger number.
