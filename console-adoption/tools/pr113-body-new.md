Approval tier: A (owner-visible UI change, declared behavior differences, removes a package)

## Summary

The in-app console now uses the Core `Nvt.Core.Avalonia.LogConsole` controls (`ConsoleHeader`, `ConsoleToolbar`, `ConsoleListView`, `ConsoleEmptyState`) over one `ConsoleController`. One Core `LogStore` (`AppConsoleLog.Store`) is the single log store. The NLog target, the console panel, the copy and save commands and the RuntimeQuery `terminal` and `terminal-links` commands all read it.

## Core 0.9.4

This PR pins `Nvt.Core` and `Nvt.Core.Avalonia` 0.9.4 (sha256 verified by `fetch-core-packages.ps1`; fonts stay 0.1.0). Core 0.9.3 reworked the console toolbar. Level filter, search, source filter and the copy, save and zoom actions now share one row, where 0.9.2 used two rows at this width. The console row height tokens are unchanged (300 DIP default, 232 DIP minimum). The list now shows all six sample rows with room to spare, so the owner can lower the default height later without losing rows. The rendered visual baseline of `MainWindow.ConsoleExpanded` was re-signed for this change.

## Before and after

Light and Dark, expanded and collapsed. The log lines are synthetic. The "after" images use Core 0.9.4. The "before" images are the old console on main.

| State | Before | After |
| --- | --- | --- |
| Light, expanded | ![before light expanded](https://raw.githubusercontent.com/Dennis40816/nvt-freeform-helper/09303f76fa9af599dec8c7050dd03e34fe02f61a/console-adoption/before/light-expanded.png) | ![after light expanded](https://raw.githubusercontent.com/Dennis40816/nvt-freeform-helper/5c9c5cf0774f6e72cfad06d3bc2a5e83451305c6/console-adoption/after-core-0.9.4/light-expanded.png) |
| Light, collapsed | ![before light collapsed](https://raw.githubusercontent.com/Dennis40816/nvt-freeform-helper/09303f76fa9af599dec8c7050dd03e34fe02f61a/console-adoption/before/light-collapsed.png) | ![after light collapsed](https://raw.githubusercontent.com/Dennis40816/nvt-freeform-helper/5c9c5cf0774f6e72cfad06d3bc2a5e83451305c6/console-adoption/after-core-0.9.4/light-collapsed.png) |
| Dark, expanded | ![before dark expanded](https://raw.githubusercontent.com/Dennis40816/nvt-freeform-helper/09303f76fa9af599dec8c7050dd03e34fe02f61a/console-adoption/before/dark-expanded.png) | ![after dark expanded](https://raw.githubusercontent.com/Dennis40816/nvt-freeform-helper/5c9c5cf0774f6e72cfad06d3bc2a5e83451305c6/console-adoption/after-core-0.9.4/dark-expanded.png) |
| Dark, collapsed | ![before dark collapsed](https://raw.githubusercontent.com/Dennis40816/nvt-freeform-helper/09303f76fa9af599dec8c7050dd03e34fe02f61a/console-adoption/before/dark-collapsed.png) | ![after dark collapsed](https://raw.githubusercontent.com/Dennis40816/nvt-freeform-helper/5c9c5cf0774f6e72cfad06d3bc2a5e83451305c6/console-adoption/after-core-0.9.4/dark-collapsed.png) |

## What the owner sees

- A new console header, filter toolbar and list with level and source filters, search with Only matches, dedupe, time modes (absolute, relative, hidden), selection copy, Copy visible and Save log.
- The console row is taller: the default is 300 DIP (was 180) and the minimum is 232 DIP (was 120). The Core header and the toolbar are 40 DIP high each and need more room than the old text console, so 180 DIP left no room for the list. The canvas above becomes shorter by the same amount. The splitter still resizes the row.
- Known visual effect of the taller console: at a 900 DIP high window the canvas viewport is about 405 DIP high (was 525 DIP). The canvas fit does not reserve room for the layer toggle row, so the column ruler numbers touch the toggles (see the "after" images). The old layout had a 7 DIP gap. This PR does not change the canvas. Options for the owner: (a) accept it, (b) lower the default console height, which only helps below about 220 DIP and leaves 4 log rows, (c) a small follow-up that adds the toggle row height to the canvas top fit margin (`PadCanvas.GetFitMargins`).
- Console width: the center column is about 790 DIP wide in a 1440 DIP window. That is the middle toolbar tier of Core 0.9.3 (700 to 1179 DIP) and is above the 640 DIP minimum.
- Source names show the full logger name and are cut with an ellipsis in the narrow layout. Proposal for owner to confirm: keep the full logger name as the source ID and register short display names (App, Workflow, Notch, Export, RuntimeQuery) with the Core `ConsoleSource` registry.

## Declared behavior differences

1. The per-panel font size buttons are gone. Console zoom with Ctrl +/- comes from Core 0.9.2 (owner decision 2026-10-13 14:37). The panel scales the row height, the column widths and the row text. It does not scale the header or the toolbar. The toolbar has the zoom out, zoom value (reset to 100%), zoom in and Jump to latest buttons. Range 80% to 300% in steps of 10%. Ctrl and plus, Ctrl and minus and Ctrl and 0 work while the list has focus. Ctrl and the mouse wheel work over the list. The old per-panel font size value (points) is gone. The zoom factor replaces it and applies to the whole list, not to the toolbar. Core does not store the zoom. `ConsoleSession.Zoom` keeps it for the session, so a new panel over the same session shows the same zoom. It is not saved across restarts (an open question in `TODO.md`).
2. Dedupe compares source, level and the full message. The timestamp is not compared (owner decision 2026-10-13).
3. Level filter, source filter, selection copy, time modes and file export are new (owner decision 2026-10-13).
4. `terminal-links` finds links with the Core scanner. It no longer checks that a path exists, and it also recognizes forward-slash paths such as `C:/work/a.txt`.
5. The Core store drops a write that exceeds its budget (10,000 entries, 4 Mi characters, 256 Ki pending characters). The old ring buffer evicted the oldest entry only.
6. The console row height tokens change (see above).
7. Copy visible, Copy selection and Save log write UTC times. The Core export has no time zone option. The list and the `terminal` command show local time.
8. A link opens only when it is an http or https URL, or an existing file or folder. A file is selected in Explorer and a folder is opened in Explorer. The old code fell back to the default handler for a file when Explorer failed. The new code never runs a file. A path is never checked when it is not a drive-letter path after normalization (`\\host\share`, `//host/share`, `/\host\share`, `\??\UNC\host\share`), because the check would send the user's credentials to that host.
9. `terminal` and `terminal-links` wait for the store to publish the writes that were accepted before the query. The old ring buffer was synchronous, so the newest lines are never missing.
10. Copy visible and Save log report a clipboard or file failure through the shell's `UiEventRunner` and do not throw on the UI thread.
11. While focus is inside the console panel, Delete, F and Ctrl+Z do not reach the canvas shortcuts (same as the old text console).
12. **A link opens on Ctrl+click or Ctrl+Enter.** A plain click only selects the row. This is the Core behavior (0.9.1 and 0.9.2). The old terminal opened a link on a plain click. Owner question: accept Ctrl+click, or ask Core for a plain-click option?
13. Link recognition follows the Core scanner. A bare file name with no path separator and no location suffix (for example `notch_review.csv`) is not a link any more. A path with a separator is a link whatever its extension, because the old extension allowlist is gone. This also changes the `terminal-links` output.
14. The repository-root search for a relative link starts from the current folder and the application folder only. The old code also started from the user profile folder. A bare name with `*` or `?` is not searched.

The `terminal` line format is unchanged: `HH:mm:ss | LEVEL | logger (56 columns) | message`, local time. New tests pin the `terminal` and `terminal-links` payload fields and offsets.

## Removed

- The AvaloniaEdit `TextEditor` terminal, `ConsoleLinkParser` and its colorizers, `AppLogStore`, `AppLogEntry`, `ShellViewModel.Console.cs` and five console partials of `FreeformHelperView`.
- The `Avalonia.AvaloniaEdit` package (csproj, `Directory.Packages.props`, two lock files).
- The console tokens, styles and colors that no code uses any more.
- `ConsoleLinkTargetSearch` and `FreeformHelperViewConsoleLinkFailureTests`. Main added them after this branch was cut, for the old link code that this PR removes. The Core scanner and `ConsoleLinkOpener` replace them (declared differences 4, 8, 13 and 14).

## Tests

- Deleted with their code: `AppLogStoreTests`, `ConsoleLinkParserTests`, `ShellViewModelConsoleBufferTests`, the old `ShellViewModelConsoleTests`, and the AvaloniaEdit-specific cases of `TerminalStartupPathTests`.
- Added: `AppConsoleLogTests`, `InAppConsoleTargetTests`, `ConsoleLinkOpenerTests`, `ConsoleTargetResolverTests`, `ConsoleSessionTests`, five `terminal` and `terminal-links` cases in `RuntimeQueryUseCaseTests`, and rewritten console cases in `ShellViewModelConsoleTests`, `TerminalStartupPathTests` and `HeadlessSessionGuardTests`.
- Tests that write to the process-wide store use their own source ID and filter by it.

## Ledgers

`LowerBaseline` passed on head `5af6584` against main `5d5dff58`: only existing debt was deleted or lowered. Nothing goes up. "Before" is main, "after" is this head.

| Ledger | Before | After |
| --- | --- | --- |
| `eng/code-health/baseline.json` findings | 1752 | 1693 |
| `eng/code-health/baseline.json` entities | 141 | 138 |
| `eng/code-health/test-debt.json` findings | 148 | 144 |
| `debt-baseline.json` `freeformViewModelStateMembers` | 96 | 95 |
| `debt-baseline.json` `processStart` | 4 | 1 |
| `debt-baseline.json` `viewModelsAvalonia` offenders | 11 | 10 |


## Verification

On head `4ef0a123fdb0d3c9d5beb476e03a5e02f5ce276f`:

- `repo-health` `LowerBaseline` and `Verify` (base = main `33073ba69e8bcf326937991c5cd36e80cedfa585`): both exit 0.
- `./scripts/tests/lint.ps1 -UseNoAppHost`: exit 0.
- Console-related tests (link opener, resolver, session, target, shell, runtime query, headless smoke, layout guard, rendered visual snapshot): all pass locally. `RenderedUiSurfaces_MatchAdvancedVisualBaseline` is green on this change with the baseline file unchanged.
- `ui-visual-minimal-baseline.json` was updated with `FH_UI_BASELINE_MODE=apply` for the new console surface.
- Full local test run on the previous head `82f33e9`: 2012 passed, 2 failed, 26 skipped. The 2 failures were the architecture ratchet tests (`DebtBaselineTests`, `LayeringBaselineTests`). Head `bd2a4f0` lowers the ledger and both pass.
- The same run showed one flaky test (`ExecuteAsync_QueryTerminalLinks_...`): it read the process-wide store while parallel tests wrote to it. Head `ae50e20` makes the query read the shell store and gives each test its own store.
- Head `e6f07d3` pins Core 0.9.2 (`Nvt.Core` and `Nvt.Core.Avalonia`, sha256 verified by `fetch-core-packages.ps1`; fonts stay 0.1.0) and wires the zoom. `LowerBaseline` and `Verify` exit 0 on it, lint exit 0, and the 74 focused console tests pass, including six new zoom tests in `ConsoleZoomTests`.
- The branch was rebased onto main `33073ba` (merged state cleanup slice 4 touches the same two ledgers). The ledger commits were merged by hand: `freeformViewModelStateMembers` 307 to 306, `stateMembers` 323 to 322. The offender lists were regenerated with the `DebtBaselineTests` measurement and are identical to the merged result. `LowerBaseline` and `Verify` (base `33073ba`) exit 0 and lint exits 0 on head `4ef0a12`. This is history: the branch was merged with the current main afterwards (see the head `5af6584` line).
- Head `5af6584`: main `5d5dff5` (PR 197 to 200) merged in, Core pinned to 0.9.4. `LowerBaseline` and `Verify` (base `origin/main`) exit 0, `./scripts/tests/lint.ps1 -UseNoAppHost` exit 0. The full local test run found one failure, `DebtBaselineTests` (the merge left the state member ceiling at 96, the offender list has 95). The ceiling is now 95 and the architecture tests pass (50 of 50). The other 2905 tests passed. Before that, the first run on this merge had found four failures that are fixed: two ledger counts, the console link offenders and the console visual baseline.
- CI runs the full test suite on this PR.


Refs #64

## Review

Independent review by a separate reviewer agent (Sonnet 5.5) that read the head. The API reviewer could not take this diff (about 430k tokens, over its 300k limit).

1. Head `4a92757`: reject (1 P1, 4 P2). Fixed in the follow-up commits (mixed-separator UNC probe, Explorer arguments for a path with spaces, command failures on the UI thread, shortcuts from a log row, test isolation and publication lag).
2. Head `d238b4c`: reject (1 P1). Fixed: an NT-prefix path such as `\??\UNC\host\share` could reach the network redirector. The resolver now probes only drive-letter paths.
3. Head `bd13e06`: reject (2 P2). Both were undeclared behavior differences (Ctrl+click, link recognition). They are declared above (items 12 to 14) and in the inventory.
4. Head `82f33e9`: no P0, P1 or P2 finding.
5. Head `bd2a4f0` (CI fix: architecture ledger lowered, typed catches in the target resolver): no finding. The reviewer compared the exception set and every ledger entry with the code change.
6. Head `ae50e20` (test flake fix: the `terminal` and `terminal-links` queries read `ShellViewModel.ConsoleStore`, the store the console panel shows): no finding.
7. Head `e6f07d3` (Core 0.9.2 pin and zoom wiring, delta only): no P0, P1 or P2 finding.
8. Head `4ef0a12` (rebase onto main `33073ba`, which includes the merged state cleanup slice 4): no finding. Eight of ten commits are identical to the reviewed ones. The two ledger commits changed. The reviewer checked every changed ceiling and all offender lines in `debt-baseline.json` against the head.

9. Head `5af6584` (merge of main, Core 0.9.4, ledgers, re-signed console snapshot): review pending.

Open P3 items: mapped network drives are probed; Copy visible and Save log are silent when no clipboard or picker is installed; no behavior test for the panel delegate release and for the shortcut routing (a source guard exists); no guard for the 640 DIP minimum width of the center column; one zoom test name says `ZoomIn` but sets `Zoom`; no test for a stale zoom write after the panel detaches.


🤖 Generated with [Claude Code](https://claude.com/claude-code)
