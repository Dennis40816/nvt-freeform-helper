# Repo Refactor Scan

Generated: 2026-03-06 21:26:36
Branch: master

## File counts
- tracked files: 530
- src: 395
- tests: 72
- docs: 31
- scripts: 19

## Line count hotspots
- docs\archive\TODO-history-2026-03-05.md: 1131
- src\FreeformHelper.UI\Styles\Controls.Form.axaml: 596
- src\FreeformHelper.UI\Styles\Tokens.axaml: 579
- src\FreeformHelper.UI\ViewModels\FreeformHelperViewModel.NotchDetails.Compensation.cs: 576
- tests\FreeformHelper.Tests\Application\Notch\NotchTableGeneratorTests.cs: 554
- src\FreeformHelper.UI\ViewModels\FreeformHelperViewModel.Persistence.cs: 551
- src\FreeformHelper.UI\Views\DevSections\DevInspectorPrototypeSectionView.axaml: 541
- src\FreeformHelper.UI\Controls\WorkspaceHeader.axaml: 512
- src\FreeformHelper.UI\ViewModels\PadInfo\CadPadInfoViewModel.Core.cs: 508
- src\FreeformHelper.UI\Controls\PadCanvas.Spatial.cs: 503
- tests\FreeformHelper.Tests\UI\Snapshots\UiLayoutGuardTests.cs: 473
- src\FreeformHelper.UI\ViewModels\FreeformHelperViewModel.PadInspectorSummary.cs: 473
- src\FreeformHelper.UI\ViewModels\FreeformHelperViewModel.Operations.cs: 469
- src\FreeformHelper.UI\ViewModels\FreeformHelperViewModel.Settings.PropertyCallbacks.cs: 466
- src\FreeformHelper.UI\ViewModels\SettingsWindowViewModel.cs: 454
- src\FreeformHelper.UI\Controls\PadCanvas.Input.Selection.cs: 443
- src\FreeformHelper.UI\Controls\PadCanvas.Properties.cs: 443
- src\FreeformHelper.UI\Views\DevSections\DevUiLabsSectionView.axaml: 435
- src\FreeformHelper.UI\ViewModels\FreeformHelperViewModel.Persistence.Project.cs: 435
- docs\reference\behavior-inventory.md: 427
- src\FreeformHelper.Application\Services\DxfRegularMappingAnalyzer.cs: 415
- tests\FreeformHelper.Tests\Application\Notch\NotchV22CompensationServiceTests.cs: 407
- src\FreeformHelper.UI\ViewModels\ShellViewModel.cs: 402
- tests\FreeformHelper.Tests\Application\Dxf\DxfPadImporterTests.cs: 400
- src\FreeformHelper.UI\Services\RuntimeQueryIpc.cs: 399

## Analyzer warning summary
- Source: `./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost`
- Result: `0 warnings`, `0 errors` (after EOL normalization on recently edited files)
- Note: `.agents/skills/repo-optimizer-loop/scripts/repo_scan.ps1` currently throws when lint warnings are empty (`$warningCodes.Count` on null/scalar). See S8.1.

## Stale-doc contract check
- Checked files:
  - `docs/reference/behavior-inventory.md`
  - `docs/guides/settings-entry-matrix.md`
- Verified contracts still documented and aligned:
  - deferred app settings persistence (`Load Project` defer, `Save Project` flush)
  - `Ctrl+S -> SaveProjectAsync() -> top toast`
  - runtime query single-entry (`RuntimeQueryUseCase.ExecuteAsync`)
- Drift candidate found:
  - Step 6 validation wording in product has been updated; docs still contain several Step 5 phrasing references in historical sections. Needs targeted doc sync (S8.6).

## S8 candidate backlog (proposal)
1. `S8.1` Fix `repo_scan.ps1` null-safe lint summary path (scanner robustness).
2. `S8.2` Split `FreeformHelperViewModel.NotchDetails.Compensation.cs` (hotspot 576 lines).
3. `S8.3` Split `FreeformHelperViewModel.Persistence.cs` by responsibility (hotspot 551 lines).
4. `S8.4` Split `CadPadInfoViewModel.Core.cs` and keep UI behavior lock tests.
5. `S8.5` Add selection/perf instrumentation around `PadCanvas` selection & visible draw pipeline (before optimization changes).
6. `S8.6` Sync docs wording/contracts for Step 6 validation naming and latest gate workflow.
