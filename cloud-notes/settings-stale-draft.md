# SETTINGS-STALE-DRAFT: cloud notes

Branch: `cloud/settings-stale-draft`, started from `origin/feature/settings/aa-pixels` (b976068). Nothing was built or run. See "What could not be verified".

## The problem on real input

Open Settings on project A, load project B (menu or `LoadProjectFromPathAsync`), press Save.
`SettingsWindowViewModel` is a full copy of project A's values. `ApplySettingsWindowDraft` compares each draft field with the live value and writes every difference, so every setting of B that differs from A is overwritten by A's value. The AA pixel counts were already guarded by `ActiveAreaPixelsDraft.ProjectIdentity`, but that guard only covered those two fields and ran after the full apply.

## Change list

- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Core.cs:81` new field `_projectGeneration`.
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.Project.cs:237` `ApplyLoadedProjectStateAsync` raises it, right after `_projectFile = file`. This is the only place a loaded project replaces `_projectFile`. The menu command and the runtime query both end here.
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.SettingsWindow.cs:284` `ResetAllSettingsToDefaults` raises it after it replaces `_projectFile.Settings`.
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.SettingsWindow.cs:21` `ProjectGeneration` (internal, read only).
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.SettingsWindow.cs:29` `TryAcceptSettingsWindowDraft(int)`: the single predicate. On a mismatch it sets the status text "Settings not saved: another project was loaded after this window opened. Reopen Settings to edit the current project." and returns false.
- `src/FreeformHelper.UI/ViewModels/SettingsWindowViewModel.cs:13,21` the draft records the generation in its constructor, so `CreateSettingsWindowViewModel()` and `new SettingsWindowViewModel(owner)` behave the same.
- `src/FreeformHelper.UI/ViewModels/SettingsWindowViewModel.cs:657-663` `OnSave` asks first. A stale draft requests the window to close and returns. Neither `ApplySettingsWindowDraft` nor `ApplyActiveAreaPixelsDraft` runs.
- `docs/guides/settings-entry-matrix.md` (SettingsWindow path line) and `ROADMAP.md` (status of SETTINGS-STALE-DRAFT) say what the code now does.

Choices worth a look:

- The check runs once, before the apply. It is not repeated between `ApplySettingsWindowDraft` and `ApplyActiveAreaPixelsDraft`. Existing tests start a load or a reset inside the apply (`SaveCommand_LoadStartedInsideDraftApply_*`, `SaveCommand_NestedDefaultsReset*`) and still expect the save to finish. The old project-identity guard in `ApplyActiveAreaPixelsDraft` is kept for that case.
- A refused Save closes the window. The status text is in the main window, and a window that stays open on stale values could only be cancelled. The edits in it belonged to project A, so nothing the user needs is lost. If you prefer to keep the window open, remove the `RequestClose?.Invoke()` call at `SettingsWindowViewModel.cs:661`.
- The generation also rises when the same file is loaded again. That is a new `ProjectFile`, and the draft would otherwise write over it.
- `ApplySettingsWindowDraft` is not changed. It carries a ledger entry (`eng/code-health/baseline.json`, symbol `...ApplySettingsWindowDraft(SettingsWindowViewModel)`), and a signature change would change its fingerprint.
- The raise in `ResetAllSettingsToDefaults` is defensive. The Reset button closes the window, so no stale draft stays open in the UI today. It is one line and matches the roadmap proposal. Drop it if you want only what real input breaks. The test `Save_AfterTheSettingsWereResetToDefaults_IsRefused` then goes too.

Not changed on purpose: a live edit of the same project while the window is open is still overwritten by the opening values on Save. That is pinned by `SettingsWindowDraftSnapshotCharacterizationTests` and needs a draft that writes only edited fields. It is a different problem, since the project is the same.

## Tests

New class `tests/FreeformHelper.Tests/UI/ViewModels/SettingsWindowStaleDraftTests.cs` (headless, collection `HeadlessUiSerial`). They use `LoadingModeTestScope`, save project B to disk, and load it through the real path.

- `Save_AfterAnotherProjectWasLoaded_LeavesTheLoadedProjectUnchangedAndSaysWhy` (theory: menu `LoadProjectCommand`, and `LoadProjectFromPathAsync`). Checks `MatchThreshold`, `EnableAutoDetectXy`, both AA counts, `HasUnsavedChanges`, undo state, the status text, and that the window asked to close once. It first asserts that the draft and B differ, so the test cannot pass by accident.
- `Save_WithADraftOpenedAfterTheLoad_AppliesTheEdit`: a draft created after the load still saves.
- `Save_AfterTheSettingsWereResetToDefaults_IsRefused`.
- `Save_WhenTheSameProjectFileIsLoadedAgain_IsRefused`.

Existing tests that save a draft right after creating it are not affected. I read the ones that touch the draft, load, or reset (`ActiveAreaPixelsSettingsTests`, `SettingsLoadingRegionBehaviorTests`, `SettingsWindowDraftSnapshotCharacterizationTests`, `ProjectLoadingInputTests`, the `SettingsPersistence` tests, `NotchGenerationStaleCompletion`). None saves a draft that was created before a load or reset.

## What ran

Only static checks: `git diff` review, CRLF check with `file` on every touched file, and a grep that `_projectFile` is assigned in one place only.

## What could not be verified

- `dotnet` and `pwsh` are not installed in the cloud container. No build, no test, no `scripts/tests/lint.ps1`, no `repo-health.ps1 -Mode Verify`. The code and the tests are not compiled. Please run `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj` and the touched classes: `SettingsWindowStaleDraftTests`, `ActiveAreaPixelsSettingsTests`, `SettingsLoadingRegionBehaviorTests`, `SettingsWindowDraftSnapshotCharacterizationTests`.
- Test assumptions that only a run can confirm: `EnableAutoDetectXy` and `MatchThreshold` round-trip through the project file unchanged, no later grid rebuild overwrites the status text before the assertion (the tests wait for the rebuild first), and `ApplyAppVisualPreferencesOnProjectLoad = false` in `CreateViewModel` does not touch these fields.
- The debt ledgers. `SettingsWindowViewModel..ctor` and `ApplyLoadedProjectStateAsync` are in or near ledger entries, and I added one line to each. If Verify reports a changed fingerprint, the fix is to move the line, not to edit the ledger by hand.
- Nothing was checked in the running app. A manual pass: open Settings, load another project from the main window, press Save, and confirm the message shows in the status bar.
