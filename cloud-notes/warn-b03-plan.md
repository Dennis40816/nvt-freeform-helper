# B-03 plan: blocking waits and Async suffix (production, 5 sites)

Analysis only. No production code was changed. Source: batch B-03 in `cloud-notes/warnings-audit.md` (branch `origin/cloud/warnings-audit`). All line numbers are for `origin/main` at `5e48bec`.

What I could not do: this cloud box is Linux with no .NET SDK, so I did not build, run the analyzers, or run any test. Everything below comes from reading the code. Where I say "I expect" or "not verified", it needs a local check.

## Summary

| # | Site | Rule | What it is | Recommended fix | Risk |
|---|------|------|-----------|-----------------|------|
| 1 | `CadLoadSpinnerProcessHost.cs:505` | VSTHRD002 | `Dispose` (app exit, UI thread) blocks on the pipe send | Shared fix S: one synchronous pipe sender | Medium |
| 2 | `CadLoadSpinnerProcessHost.cs:629` | VSTHRD002 | `Show` (UI thread) blocks on the pipe send while holding a lock | Shared fix S | Medium |
| 3 | `CadLoadSpinnerProcessHost.cs:665` | VSTHRD200 | `GetLatestStateSyncTask()` has no `Async` suffix | Rename, and do it together with the B-05 entry for the same line | Low, but see ledger trap |
| 4 | `CadLoadSpinnerProcessHost.cs:735` | VSTHRD002 | Hide path blocks a thread-pool thread on the pipe send | Shared fix S | Low |
| 5 | `FreeformHelperViewModel.Persistence.Project.cs:16` | VSTHRD200 | `TryStartSaveProjectCommand()` returns `Task<bool>?` with no `Async` suffix | Rename to `TryStartSaveProjectCommandAsync` | Low |

Sites 1, 2 and 4 are the same root cause: one async method (`TrySendRequestAsync`) is called from code that cannot await. AGENTS.md says "one root cause, one general rule", so I plan one change for the three (shared fix S) instead of three patches.

Metric this moves: production warning count, 5 sites in B-03 (3 x VSTHRD002, 2 x VSTHRD200). If site 3 is done with B-05, a 6th site (VSTHRD003 at the same line) goes too.

---

## Background: how the spinner host sends messages

`CadLoadSpinnerProcessHost` starts a second copy of the app as a small spinner window. It talks to it over a named pipe. `TrySendRequestAsync` (line 542) opens a pipe client, connects with a 250 ms timeout (`ConnectTimeoutMs`), writes one JSON line, and returns true or false.

Three sync entry points call it with `.GetAwaiter().GetResult()`:

- `SendRequest` (line 501), used only by `Dispose` for the Shutdown message.
- `SendShowRequestCoreLocked` (line 625), used by `Show` on the UI thread and by the background state sync. The caller holds `lock (_sync)`.
- `TrySendRequest` (line 731), used by `SendHideRequest` (line 712), which is only called from `SyncRequestedVisibilityStateAsync` on a thread-pool thread.

One async caller remains: `WaitForServerReadyAsync` (line 478, ping loop at start-up, on a thread-pool thread via `Task.Run(_startProcess)`).

Facts that matter for every site:

- The tests never touch the real pipe. They all pass `_sendRequestOverride`, which makes `TrySendRequestAsync` return synchronously (line 544). So the real pipe code has no automated coverage.
- The spinner window watches its parent process (`CadLoadSpinnerWindow.axaml.cs:83-88`, from the `--parent-pid` argument).
- I found no other readers of the pipe protocol besides `CadLoadSpinnerWindow.axaml.cs` (server side) and this host.

---

## Site 1: `CadLoadSpinnerProcessHost.cs:505` (VSTHRD002, `Dispose` path)

```csharp
private void SendRequest(CadLoadSpinnerRequest request, int timeoutMs, bool swallowIoErrors = false)
{
    try
    {
        var ok = TrySendRequestAsync(request, timeoutMs).GetAwaiter().GetResult();
        ...
```

Called from `Dispose()` (line 162) with `ShutdownCommand`, right before `CleanupProcess(forceKillIfStillRunning: true)`. `Dispose` runs from `desktop.Exit` in `App.axaml.cs:132-135`, on the UI thread.

What could break in real use:

- Exit freeze. With the spinner process dead or busy, the UI thread waits up to the 250 ms connect timeout, plus any wait for `_sync` if the background sync is holding it. Bounded (under about half a second), but the window is already closing, so the user sees a hang on exit.
- Deadlock risk is latent, not live. The method uses `ConfigureAwait(false)` on the connect and the write, so the UI context is not needed to finish. But `await using var writer` (line 557) awaits `DisposeAsync` without `ConfigureAwait(false)`. If the code reaches that line still on the UI thread and `DisposeAsync` does not finish at once, its continuation is posted to the blocked UI thread: a classic deadlock. I expect it is safe today because `NamedPipeClientStream.ConnectAsync` runs the connect on another thread, so the code is on a pool thread by then. That is an implementation detail of .NET that I have not verified, and the code does not guarantee it.
- The Shutdown message is almost pointless. The next line force-kills the process, with no wait in between, and the child watches the parent PID anyway.

Option A (shared fix S, recommended): replace the async sender with one synchronous sender (details below). The three blocking waits disappear because nothing is async any more.
- Risk: medium. It changes the real pipe code path, which no test covers. Exceptions stay the same set (`TimeoutException`, `IOException`, etc.), and `swallowIoErrors` keeps its meaning. Needs a manual run on Windows (see "Manual check").

Option B: stop sending Shutdown in `Dispose` and rely on the force-kill (plus the parent-PID watcher). Then `SendRequest` and its `swallowIoErrors` machinery can be deleted.
- Risk: medium. It removes a protocol step and changes exit behavior. If the kill fails (the code catches and logs that), the child no longer gets a polite request first. Also, `ShutdownCommand` stays handled on the server side, so there is dead protocol to clean up later. It fixes only this one site; sites 2 and 4 still need a fix.

Recommendation: A. Option B can be a later simplification, once A has shown the exit path is fine.

---

## Site 2: `CadLoadSpinnerProcessHost.cs:629` (VSTHRD002, `Show` path)

```csharp
private bool SendShowRequestCoreLocked(CadLoadSpinnerRequest request)
{
    try
    {
        var ok = TrySendRequestAsync(request, ConnectTimeoutMs).GetAwaiter().GetResult();
```

Callers: `Show` (line 117) on the UI thread inside `lock (_sync)`, and `SyncRequestedVisibilityStateAsync` (line 346) on a pool thread inside `lock (_sync)`.

What could break in real use:

- UI freeze. `Show` is called when a CAD load starts (`FreeformHelperView.CadLoadSpinner.cs:88-104`). If the spinner process exists but the pipe server is not listening yet (just started, or between two connections: the server creates a new pipe instance per message), the UI thread waits up to 250 ms for each attempt, with a lock held. The spinner is meant to appear at the moment the UI is busy, so this is the worst time to stall.
- Lock held across I/O. While one thread waits on the pipe inside `lock (_sync)`, every other `_sync` user (`Hide`, `HasRunningProcess`, `GetVisibilityStateSnapshot`) waits too.
- Same latent deadlock as site 1 if the thread is the UI thread and the code ever continues on the UI context.

Option A (shared fix S, recommended): same synchronous sender. This removes the async-over-sync pattern and the deadlock class. It does not remove the up-to-250 ms wait, because that is how `Show` works today.
- Risk: medium. Same untested pipe path as site 1. Keeps the "visible immediately after `Show`" behavior that tests assert (`CadLoadSpinnerProcessHostTests`, `Assert.Equal("Visible", ...Effective)` right after `Show`).

Option B: make `Show` never send inline. It would only record the request and queue the state sync, which sends from the pool thread with `await`, outside the lock (re-check `_disposed` afterwards). This also removes the UI-thread wait.
- Risk: high. It changes who sets `_effectiveVisibility` and when, so several tests need rewriting (they expect `Visible` straight after `Show`). It opens a race: a send can land just after `Dispose`. It adds a thread-pool hop before the spinner appears. It is a behavior redesign, not a warning fix.

Recommendation: A for this batch. If the 250 ms stall is ever seen in real use, Option B is a separate task with its own tests.

---

## Site 3: `CadLoadSpinnerProcessHost.cs:665` (VSTHRD200, missing `Async` suffix)

```csharp
internal Task GetLatestStateSyncTask() => _stateSyncTask;
```

It is an `internal` accessor so tests can await the last background sync. Nothing in `src` calls it. Readers (searched all of `origin/main`, including `.axaml`, scripts, docs and strings):

- 6 call sites in 3 test files: `CadLoadSpinnerProcessHostFailureTests.cs` (lines 96, 111), `CadLoadSpinnerProcessHostRestartTests.cs` (78, 109), `CadLoadSpinnerProcessHostTests.cs` (84, 112).
- `eng/code-health/baseline.json`: two entries for this member (VSTHRD003 and VSTHRD200, same fingerprint).

What could break in real use: nothing at run time. It is only a name. The risk is the debt ledger.

Ledger trap: the audit lists this same line twice: VSTHRD200 in B-03 and VSTHRD003 in B-05. The ledger fingerprint includes the member name (AGENTS.md "Debt ledger rules", point 2). Any rename or change into a property moves both entries. The VSTHRD200 entry goes away, but I expect the VSTHRD003 finding to reappear under the new name as a "new" finding, and Verify would fail. So this site cannot be cleanly done in B-03 alone. (Not verified: I could not run the analyzer to see whether VSTHRD003 still fires after a rename.)

Option A (recommended): rename to `GetLatestStateSyncTaskAsync` and update the 6 test call sites. Do it in the same change as the B-05 entry for this member, and lower the ledger with the script, not by hand.
- Risk: low for the code. Medium for process: the batch boundary has to move (see below). A small downside: an "Async" getter that just returns a field is a slightly odd name.

Option B: turn it into an `internal Task LatestStateSyncTask => _stateSyncTask;` property. VSTHRD200 only checks methods, so the rule cannot fire. Test call sites lose the `()`.
- Risk: low for the code. Unknown whether VSTHRD003 still fires on a property; it gets the same ledger trap as A. Needs one build to find out.

Recommendation: A, because the effect on the analyzer is predictable. Whichever is picked, move this entry out of B-03 into B-05 (or merge the two) so a single PR removes both findings.

---

## Site 4: `CadLoadSpinnerProcessHost.cs:735` (VSTHRD002, hide path)

```csharp
private bool TrySendRequest(CadLoadSpinnerRequest request, int timeoutMs)
{
    try
    {
        return TrySendRequestAsync(request, timeoutMs).GetAwaiter().GetResult();
```

The only caller is `SendHideRequest`, which is only called from `SyncRequestedVisibilityStateAsync` (lines 300 and 422). That method runs on a thread-pool thread (`Task.Run` in `QueueStateSync`) and calls it outside any lock.

What could break in real use: very little.
- No UI thread, so no deadlock.
- It blocks one thread-pool thread for up to 250 ms. The thread is already dedicated to this sync.
- Hide is delayed by the same wait while the spinner process is slow, which is not user-visible.

Option A (shared fix S, recommended): becomes a direct call to the synchronous sender.
- Risk: low. Same untested pipe path as sites 1 and 2, but a failed hide only logs and is retried by the next sync.

Option B: make `SendHideRequest` and `TrySendRequest` async and `await` them from `SyncRequestedVisibilityStateAsync`. This is the idiomatic fix for this one site.
- Risk: low in isolation. But it means keeping the async sender next to the sync one that sites 1 and 2 need, so there would be two copies of the pipe code and the "one path" rule is broken. If site 1 and 2 were solved with Option B (no inline send), this would be the right choice, but I do not recommend that for them.

Recommendation: A, for consistency with sites 1 and 2.

---

## Shared fix S (sites 1, 2, 4)

- Replace `TrySendRequestAsync` with one synchronous method with a different name, for example `WriteRequestToPipe` (not `TrySendRequest`, see warning below). Sketch: `new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.None)`, `client.Connect(timeoutMs)`, `StreamWriter` with the same settings (UTF-8 without BOM, `AutoFlush`), `WriteLine(payload)`, return true. Keep the `_sendRequestOverride` branch and the two catch blocks (`TimeoutException`, `IOException` return false).
- `SendRequest`, `SendShowRequestCoreLocked` and `TrySendRequest` call it directly; their `.GetAwaiter().GetResult()` goes away.
- `WaitForServerReadyAsync` (line 483) is the one async caller. It already runs on a thread-pool thread, so it can call the sync method directly, or `await Task.Run(() => ...)` if we want the async method not to block at all. I would use `Task.Run`, so no new blocking-in-async warning (VSTHRD103-style) is possible.
- Naming warning: if a sync method and an `...Async` method with the matching name both exist in the type, calling the sync one from an async method triggers VSTHRD103, which would add a new finding (the ledger only goes down). Using a name that is not the pair of the old one, and deleting the old async one, avoids that. Not verified by build.
- The old server side (`CadLoadSpinnerWindow.axaml.cs`) is unchanged: it uses `PipeOptions.Asynchronous` on its end, and a client with `PipeOptions.None` can still connect to it.
- Ledger: the `SendRequest`, `SendShowRequestCoreLocked` and `TrySendRequest` members keep their names and signatures, so only their VSTHRD002 entries should disappear. I could not check the syntax hash, so lower the ledger with the script after the build.

Net effect of S: 3 VSTHRD002 findings gone, the async-over-sync deadlock pattern gone, and the unawaited `DisposeAsync` context hazard gone. It does not shorten the up-to-250 ms wait on the UI thread in `Show` and `Dispose`.

---

## Site 5: `FreeformHelperViewModel.Persistence.Project.cs:16` (VSTHRD200)

```csharp
internal Task<bool>? TryStartSaveProjectCommand()
{
    if (SaveProjectCommand.IsRunning || !SaveProjectCommand.CanExecute(null))
    {
        return null;
    }

    // The save command preserves the Task<bool> returned by SaveProjectAsync.
    return (Task<bool>)SaveProjectCommand.ExecuteAsync(null);
}
```

What could break in real use: nothing at run time. Only the name changes.

Readers (searched all of `origin/main`, including `.axaml`, scripts, tests, reflection strings):

- `src/FreeformHelper.UI/MainWindow.axaml.cs:105` (save before close).
- `src/FreeformHelper.UI/Views/FreeformHelperView.InputAndShortcuts.cs:104` (Ctrl+S).
- `docs/reference/behavior-inventory.md:437` (text mention).
- `eng/code-health/baseline.json:6363` (one VSTHRD200 entry; no other entry for this member).

No test reads it.

Option A (recommended): rename to `TryStartSaveProjectCommandAsync`, update the 2 callers and the one doc line. The ledger entry for the old name disappears and no other finding is attached to this member, so there is no new fingerprint.
- Risk: low. Must be done in one commit with the ledger lowered by the script.

Option B: make the method return a non-null `Task<bool>` so it is a normal `...Async` method (for example `Task.FromResult(false)` when the save cannot start).
- Risk: medium. Today `null` means "did not start" and `false` means "started and failed". The Ctrl+S handler returns silently on `null` but shows a "Save project failed" toast on `false`. Merging them would add a wrong toast when a save is already running. It would need a separate result type.

Recommendation: A.

---

## Suggested slicing and order

1. Slice 1 (low risk, independent): site 5 rename. Files: ViewModel, `MainWindow.axaml.cs`, `FreeformHelperView.InputAndShortcuts.cs`, `behavior-inventory.md`. Ledger via script. Run `lint.ps1 -UseNoAppHost`.
2. Slice 2: shared fix S for sites 1, 2, 4 in `CadLoadSpinnerProcessHost.cs` only. Run the spinner host tests, then do the manual check below.
3. Slice 3: site 3 together with the B-05 entry for the same member (rename + 6 test call sites).

Suggested change to the audit's batching: move line 665 VSTHRD200 from B-03 into B-05, so B-03 is 4 entries and B-05 is 3 entries covering the same member; or leave B-03 and make B-05 a hard dependency. The audit already says B-03 onward should wait for the owner's choice (section 6).

## Manual check needed (cannot be done in the cloud)

AGENTS.md says "real inputs first". Needed on Windows after slice 2, with a real spinner process, not the test override:

1. Start the app, load a CAD file: the spinner appears and goes away when loading finishes.
2. Load two files back to back: no stuck spinner.
3. Kill the spinner process from Task Manager while idle, then load a CAD file: a new spinner starts (the restart path).
4. Close the app: it exits promptly and no orphan spinner process is left in Task Manager.

Optionally measure the wait in `Show` and `Dispose` before and after, for the record.

## Open questions for the owner

- Is the up-to-250 ms UI wait in `Show` acceptable for now? If not, Option B for site 2 is a separate, larger task.
- OK to drop the Shutdown message in `Dispose` as a follow-up (site 1 Option B)?
- Prefer rename (A) or property (B) for site 3, and OK to move it into B-05?
