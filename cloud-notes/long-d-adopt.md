# Adoptable branch: long-d plus the long-e remainder

Branch: `cloud/long-d-adopt` (from `origin/main` at 5e48bec). No PR. Nothing pushed to `main`.

Source branches (actual names): `origin/cloud/long-d-decomp` and `origin/cloud/long-e-plan`.
Both were cut from the same `main` commit (5e48bec), which is still the tip of `origin/main`,
so the long-d commits applied with no conflict.

## What came from long-d

Three commits cherry-picked in order (content identical to the source branch):

| Commit here | Source | Content |
|---|---|---|
| refactor(ui): split batch D long async and settings-order methods | 6bfb819 | 17 long methods split into helpers, 15 source files |
| fix(architecture): keep the simulation partial free of ViewModels refs | 3df368c | moves two payload builders so `Commands.Simulation.cs` gains no ViewModels reference |
| docs(notes): correct the batch D passed-test count | 7ad973a | edits `cloud-notes/long-d-decomp.md` |

`cloud-notes/long-d-decomp.md` is kept as is. It has the per-method before/after table.

## What long-e contributed

`cloud/long-e-plan` has two commits. Only part of it is code:

| Item | In long-e as | Covered by long-d? | Decision |
|---|---|---|---|
| 1. `QueryExportNotchAsync` split (124 -> 69 lines) | code, commit 7e9f976 | Yes. long-d splits the same method differently (124 -> 60 lines) | **Skipped.** Both rewrite the same method, so they conflict; long-d is the base. |
| Two tests `ExecuteAsync_QueryExportNotch_RejectsMissingPath` and `..._CreatesOutputFolderBeforeExport` | code, same commit | No | **Added**, as one commit (tests only). They pass on `main`'s original method and on the long-d version. |
| 2. `LoadProjectFromPathCoreAsync` | plan text only | Yes (long-d item 11) | Nothing to add. |
| 3. `ApplyLoadedProjectStateAsync` | plan text only | Yes (long-d item 12) | Nothing to add. |
| 4. `RunDeferredCadInspectorSnapshotRefreshAsync` | plan text only | Yes (long-d item 8) | Nothing to add. |
| 8. `SyncRequestedVisibilityStateAsync` | plan text only | Yes (long-d item 1) | Nothing to add. |
| 9. `GenerateCurrentNotchTableAsync` | plan text only | Yes (long-d item 7) | Nothing to add. |
| 5. `NotchTableGenerator.BuildV22CadCandidate` (149) | plan text only | **No** | **Not added.** See below. |
| 6. `NotchTableGenerator.BuildCanonicalCandidatesByDiff` (160) | plan text only | **No** | **Not added.** See below. |
| 7. `NotchV22CompensationService.RunStageCEvaluateToFullAndDiagnostics` (172) | plan text only | **No** | **Not added.** See below. |
| 10. `PadCanvas.Render` (306) | plan text only | **No** | **Not added.** See below. |

### The count of "4 items" checked

The earlier comparison of 4 is confirmed, with one correction in what the 4 are.
Of the 10 batch E methods, 6 are covered by long-d (1, 2, 3, 4, 8, 9). The 4 that long-d
does not cover are 5, 6, 7 and 10. But **long-e has no code for these four**. It has
only a written extraction plan for each, in `cloud-notes/long-e-plan.md` on
`origin/cloud/long-e-plan`. The only code long-e has is method 1 and the two tests.

So there is nothing to cherry-pick for items 5, 6, 7 and 10. Writing the
refactors from the plan would be new work, not adoption, and these are the
medium-high and high risk methods in that plan (parallel loops with shared timing
accumulators, per-regular Stage C timers, a 306-line render method). The golden
comparisons they need (Stage C / v2.2) and a Dev page render check cannot run in
this environment: the `example/` submodule is private and not available. They were
left for a separate batch with that data.

The plan stays available on `origin/cloud/long-e-plan`. Do not delete that branch
until items 5, 6, 7 and 10 are done or moved to `ROADMAP.md`.

## Debt baselines

- `eng/code-health/baseline.json` and `eng/code-health/test-debt.json`: **not touched**.
- `tests/FreeformHelper.Tests/Architecture/debt-baseline.json`: changed by long-d. The
  entry count of every category is the same as on `main` (checked per category).
  Seven `file:line` keys moved with the code they point to (fileWriteAllText x2,
  processStart x1, directClockReads x4). Nothing was added or raised, but this
  is a re-key, not a pure removal. It cannot be avoided: the ratchet matches by
  line, and the code moved.
- The 17 long-method entries in `eng/code-health/baseline.json` should now drop (see the list in
  `long-d-decomp.md`). That must be done by the script, not by hand: run
  `repo-health.ps1 -Mode LowerBaseline`, then `-Mode Verify`, on a machine with PowerShell.

## What ran

Environment: Linux, no `pwsh`. .NET SDK 10.0.301 (the version in `global.json`) was
installed to a scratch folder outside the repo; Core packages came from
`python scripts/fetch_core_packages.py`.

- `dotnet build FreeformHelper.sln`: 0 errors, 352 warnings. The warning set was not
  diffed against `main` in this session. long-d's own notes report no new warning
  against `main`.
- `dotnet test --filter QueryExportNotch`: 5 passed, 0 failed (includes the 2 added tests).
- Full test project: see the result line below.

- Full test project (`FreeformHelper.Tests`): 2,944 passed, 43 skipped, 3 failed, 2,990 total.
  The 3 failures also fail the same way on an unchanged `origin/main` worktree (checked
  by running those 3 tests there), so they are not from this branch. They are Linux
  environment differences:
  - `FreeformHelperViewModelTests.ExportDxfVisibleCommand_WhenDxfRegularSourceLayerSelected_IncludesRegularLayerPads`
    (expects CRLF in DXF output; long-d's notes report the same)
  - `PadInfoViewModelTests.CadPadInfo_UsesSharedNotchDisplayProjectionTexts` and
    `NotchDisplayProjectorTests.Build_FromPadInspectorSnapshot_ProducesSharedDisplayProjection`
    (expect `94.10%`, get `94.10 %`; a percent-format difference on this machine)
  The Architecture ratchet tests are part of this run and passed.

Not run, because `pwsh` is not available here:

- `eng/core-health/repo-health.ps1 -Mode Verify` and `LowerBaseline`
- `scripts/tests/lint.ps1` and `scripts/dev/prepare-ui-workspace.ps1`

Changed text files were checked: all keep CRLF line endings.

## Before opening a PR

1. Run `lint.ps1 -UseNoAppHost` and the repo-health Verify command from `AGENTS.md`.
2. Run `LowerBaseline` so the 17 method entries leave `baseline.json`.
3. Run one `query export-notch --format csv --path <tmp>` on a real project. The
   success path of the export still has no automated test (needs `example/`).
