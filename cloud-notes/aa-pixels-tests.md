# cloud/aa-pixels-tests

Base: `origin/feature/settings/aa-pixels` (PR 260 head `b976068`). No production code changed.

## Added

- `tests/FreeformHelper.Tests/UI/ViewModels/ActiveAreaPixelsSettingsProjectLoadTests.cs`
  - `StaleSettingsDraft_SavedAfterLoadingProjectB_KeepsProjectBPixelCounts` (theory, 3 cases)
  - Project A (6480 x 848) and project B (3240 x 424) are saved to real files. A is loaded through
    `LoadProjectFromPathAsync` and a Settings window view model is opened on it. X is left untouched,
    cleared to 0, or edited to 100. B is then loaded through `LoadProjectFromPathAsync`, and the old
    draft is saved. B must keep 3240 x 424.
- `tests/FreeformHelper.Tests/Application/Project/ActiveAreaPixelsZeroReloadTests.cs`
  - `SettingsZero_SavedToFileAndReloaded_ReadsAsNotSetWithoutAZeroInTheJson`
  - X is set to 0 in the Settings draft and saved. The project is saved to a temp file and reloaded with
    `JsonProjectStore.Load`. X must be null, Y must stay 848, and the `activeAreaPixels` JSON section must
    have no `x` value and no numeric 0.

Both classes carry `[Collection("HeadlessUiSerial")]`, as `SettingsLoadingRegionBehaviorTests` does for
headless Avalonia tests, and reuse `LoadingModeTestScope`. Files use CRLF.

## What ran

Nothing. The cloud container has no `dotnet` (and no `pwsh`), so the tests were **not built and not run**.
The code was written by reading `ApplyActiveAreaPixelsDraft`, `NormalizeActiveAreaPixels`,
`ActiveAreaPixelsDraft` and `JsonProjectStore` only. Treat it as unverified until run locally:

```powershell
dotnet test tests/FreeformHelper.Tests --filter "FullyQualifiedName~ActiveAreaPixelsSettingsProjectLoadTests|FullyQualifiedName~ActiveAreaPixelsZeroReloadTests"
```

`./scripts/tests/lint.ps1 -UseNoAppHost` was not run either.

## Notes for the reviewer

- The existing `AssertEditedDraftDoesNotOverwriteALoadedProjectAsync` already clears X and loads through
  `LoadProjectFromPathAsync`. The new theory overlaps with it on the "cleared X" case. It differs by loading
  project A from a real file, using two separate files, and adding the "untouched" and "edited to 100" cases.
  Drop the overlap if it is unwanted.
- Based on the code, no production bug is expected: a draft whose `ProjectIdentity` is not the current
  `_projectFile` is dropped, and a value below 1 normalizes to `null`.
