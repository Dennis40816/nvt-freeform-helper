# Review of PR #260: optional AA pixel counts in Settings

- PR: https://github.com/Dennis40816/nvt-freeform-helper/pull/260 (`feature/settings/aa-pixels`)
- Head reviewed: `287d220d15ced784783d47bab05955b3a8e44d6f` ("fix(settings): address review of the AA pixel fields")
  - I started on `cc70bb8408202672e9cc29b59dedacad1be5bf54`. The author pushed `287d220` while I worked, and I reviewed that head again.
- Merge base with `main`: `5e48bec842d9f0e08ac7d9f6ac64abe801954782`
- Review date: 2026-11-05 (scheduled cloud run)

## Verdict: accept

No P1. Two P2 findings remain on `287d220`. The P2-1 fix is one line and should go in before merge. P2-2 comes from an older problem that affects every Settings field, so it should be fixed separately.

## What I ran

- .NET SDK 10.0.301, after `python scripts/fetch_core_packages.py`.
- `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`: 0 errors, 86 warnings. The warning count is the same on both heads, and none of the warnings is in a file the PR changed.
- `dotnet build tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj`: 0 errors. There are no warnings in `ActiveAreaPixel*`, `NumberScrubber.Properties.cs` or `SettingsGeneralSectionView.axaml`.
- I ran the related tests with this filter: `ActiveAreaPixel|SettingsWindow|ProjectLoad|JsonProjectStore|UiLayoutGuard|NumberScrubber`. On `287d220`, 101 passed and 0 failed. On `cc70bb8`, 95 passed and 0 failed.
- I also ran four temporary probe tests. They load real saved project files through `LoadProjectFromPathAsync`, using `LoadingModeTestScope`. They are not committed. Their results are listed in the findings below.
- **Skipped:** `eng/core-health/repo-health.ps1 -Mode Verify` and `scripts/tests/lint.ps1`. `pwsh` is not available in this environment. Someone must run them before merge.
- I did not check the `example/` submodule. Real-input tests on example data did not run.

## Status of the points I was asked to check

| Point | `cc70bb8` | `287d220` |
|---|---|---|
| Stale Settings draft overwrites another project's values | Bug: an untouched draft cleared project B's counts (6480 to 0) | Partly fixed. Untouched fields are kept. A field edited before the load still overwrites B (P2-2) |
| Float compare at exactly 2 % | Bug: `Evaluate(98,100,100,100)` gives a relative difference of 0.020000000000000018 and is reported as `PitchMismatch` | Fixed with a 1e-9 tolerance and tests on both sides of the threshold |
| Null round trip through save and load | Missing field and `x/y = null` load correctly. `"activeAreaPixels": null` crashes | Not fixed (P2-1) |
| Accessible names on the new inputs | Missing | Fixed. `AutomationProperties.Name` is forwarded to the inner `TextBox`. `_inputBox` is set in the constructor, before XAML assigns the name, so forwarding works |
| Tooltip accuracy | Wrong: it said "Used by image export", but no code reads the counts | Fixed: "Stored with the project. Nothing reads it yet." |
| Test for "open Settings, press OK with no changes, nothing rebuilds" | Missing | Still missing (P3-1). The behavior is correct: my probe passed |

## Findings

### P2-1: A project file with `"activeAreaPixels": null` crashes when Settings opens

- Where: `src/FreeformHelper.Infrastructure/Project/ProjectFileMigrator.cs:63`, where the line is missing. The crash happens at `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Settings.PropertyCallbacks.Step1.cs:522` and `:544`.
- Failure input: a project JSON with `"settings": { ..., "activeAreaPixels": null }`. NFH does not write this itself, but a hand edit, a script or another tool can. The load succeeds. Then `CreateSettingsWindowViewModel()` reads `ActiveAreaPixelsX` and throws `NullReferenceException`. I reproduced this on `287d220`, and the stack trace ends in `get_ActiveAreaPixelsX()`. Saving such a project also writes `null` back.
- Why it matters: `EnsureSettingsNotNull` guards every other settings sub-object (`Grid`, `Matching`, `View`, `Notch`, `IndexMapping`) for exactly this case. The new sub-object skipped that rule.
- Suggested fix: add `settings.ActiveAreaPixels ??= new ActiveAreaPixelsSettings();` to `EnsureSettingsNotNull`. Add one test to `ActiveAreaPixelsPersistenceTests`: set the node to `null`, load, and assert that both counts read as not set.

### P2-2: An edited field in a stale Settings window still overwrites the project loaded after it opened

- Where: `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Settings.PropertyCallbacks.Step1.cs:571-572` (`IsPixels*Edited` compares against the values loaded into the draft, not against the current project) and `src/FreeformHelper.UI/ViewModels/SettingsWindowViewModel.cs:656`.
- Failure input: open Settings on project A (no counts). Type AA pixels X = 1920. Leave the window open; it is not modal (`FreeformHelperView.SettingsWindow.cs:52`, `Show(owner)`). From the main window, load project B, which is saved with 6480 x 848. Press Save in the old window. B now holds X = 1920 and Y = 848, a value typed for A, and the status shows a pitch mismatch. I reproduced this on `287d220` (expected 6480, actual 1920).
- Scope: this comes from an older problem. The same Save writes all of A's draft values into B. A probe with `MatchThreshold` fails the same way (expected 0.27, actual 0.35). The PR's narrow guard fixes only the untouched pixel fields. Per the "one root cause, one general rule" rule, the fix belongs to the Settings window as a whole, not to the pixel fields.
- Suggested fix, general: the owner keeps a project generation number that it increases in `ApplyLoadedProjectStateAsync` and `ResetAllSettingsToDefaults`. `SettingsWindowViewModel` records it when it is created. `OnSave` refuses, with a status message, or the view closes or reloads the Settings window when the number changes. Then the narrow `IsPixels*Edited` guard can go. If that is out of scope for this PR, record it in `ROADMAP.md` and state the remaining risk in the PR.
- Test gap: `SettingsWindowDraft_SavingUntouchedKeepsCountsOfAProjectLoadedAfterItOpened` (`tests/.../ActiveAreaPixelsSettingsTests.cs:86`) imitates the load with property setters. It does not load a file. A test through `LoadingModeTestScope` + `LoadProjectFromPathAsync` would cover the real path.

### P3-1: No test for "press OK with no changes: nothing rebuilds and the project stays clean"

- Where: `tests/FreeformHelper.Tests/UI/ViewModels/ActiveAreaPixelsSettingsTests.cs`.
- Failure input: none today. My probe loaded a saved project with 6480 x 848, opened Settings, pressed Save with no edits and saw no `PropertyChanged` on the owner and `HasUnsavedChanges == false`. Nothing guards this, though. The draft rounds values (`decimal` to `Math.Round` to `int?`), and a later change to that path could make an untouched OK mark the project dirty or start a rebuild.
- Suggested fix: add this test. Load a saved project with counts, wait for the rebuild to go idle, set a status sentinel, run `CreateSettingsWindowViewModel().SaveCommand`, then assert that the sentinel is unchanged, that `HasUnsavedChanges` is false and that the owner raised no property changes.

### P3-2: One Save that changes both counts adds two undo entries

- Where: `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Settings.PropertyCallbacks.Step1.cs:533`, `:555` and `:571-572`.
- Failure input: change X and Y in Settings and press Save. Ctrl+Z reverts only Y, and a second Ctrl+Z reverts X. The other Settings fields are applied inside `_settingsSuppression.EnterSettingsLoading()` and add no undo entry. The new fields behave differently.
- Suggested fix: apply both counts inside the same settings-loading scope, which marks the project dirty explicitly, or push one undo entry for both. Low priority; this can go to the backlog.

## Not checked

- `repo-health.ps1 -Mode Verify` and `lint.ps1`, because `pwsh` is not available.
- Example-data and golden tests, because the `example/` submodule was not initialized.
- The visual look on the Dev page and the headless tooltip-open smoke test. The PR changes tooltip text only, not tooltip style.
