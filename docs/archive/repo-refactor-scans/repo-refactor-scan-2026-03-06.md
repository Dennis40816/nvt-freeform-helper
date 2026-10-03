# Repo Refactor Scan

Generated: 2026-03-06 08:28:34
Branch: master

## File counts
- tracked files: 519
- src: 386
- tests: 72
- docs: 29
- scripts: 19

## Line count hotspots
- docs\archive\TODO-history-2026-03-05.md: 1131
- src\FreeformHelper.UI\Styles\Controls.Form.axaml: 596
- src\FreeformHelper.UI\Controls\NumberScrubber.axaml.cs: 592
- src\FreeformHelper.UI\ViewModels\FreeformHelperViewModel.Operations.LayerSelection.cs: 591
- src\FreeformHelper.UI\Styles\Tokens.axaml: 579
- src\FreeformHelper.UI\ViewModels\FreeformHelperViewModel.NotchDetails.Compensation.cs: 576
- src\FreeformHelper.Application\Services\NotchTableGenerator.Generation.cs: 574
- src\FreeformHelper.UI\ViewModels\FreeformHelperViewModel.Persistence.cs: 551
- src\FreeformHelper.UI\Views\DevSections\DevInspectorPrototypeSectionView.axaml: 541
- tests\FreeformHelper.Tests\Application\Notch\NotchTableGeneratorTests.cs: 519
- src\FreeformHelper.UI\Controls\WorkspaceHeader.axaml: 512
- src\FreeformHelper.UI\ViewModels\PadInfo\CadPadInfoViewModel.Core.cs: 508
- src\FreeformHelper.UI\Controls\PadCanvas.Spatial.cs: 503
- src\FreeformHelper.UI\ViewModels\FreeformHelperViewModel.PadInspectorSummary.cs: 473
- src\FreeformHelper.UI\ViewModels\FreeformHelperViewModel.Operations.cs: 469
- tests\FreeformHelper.Tests\UI\Snapshots\UiLayoutGuardTests.cs: 467
- src\FreeformHelper.UI\ViewModels\FreeformHelperViewModel.Settings.PropertyCallbacks.cs: 466
- src\FreeformHelper.UI\ViewModels\SettingsWindowViewModel.cs: 454
- src\FreeformHelper.UI\Controls\PadCanvas.Properties.cs: 443
- src\FreeformHelper.UI\Controls\PadCanvas.Input.Selection.cs: 443
- src\FreeformHelper.UI\ViewModels\FreeformHelperViewModel.Persistence.Project.cs: 435
- src\FreeformHelper.UI\Views\DevSections\DevUiLabsSectionView.axaml: 435
- docs\reference\behavior-inventory.md: 427
- src\FreeformHelper.Application\Services\DxfRegularMappingAnalyzer.cs: 415
- tests\FreeformHelper.Tests\Application\Notch\NotchV22CompensationServiceTests.cs: 407

## Analyzer warning summary（UI project）
- Source: `build/logs/s7-analyzer-build.log`
- Command: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
- Result: no warning codes detected (`NO_WARNINGS`)

## Full-repo lint status（`lint.ps1 -AllFiles`）
- Result: **failed**
- Primary blocker: `ENDOFLINE` violations across multiple projects/files.
- Observation:
  - Current milestone gates (`lint.ps1` changed-files scope) are green.
  - Full-repo formatting/lint baseline is not yet clean, and should be treated as a dedicated backlog item.

## Stale behavior/doc contract check
- Checked files:
  - `docs/reference/behavior-inventory.md` (last updated 2026-03-05)
  - `docs/guides/settings-entry-matrix.md` (last updated 2026-03-05)
- Contract spot-check:
  - deferred app settings policy: present in both docs
  - `Ctrl+S -> SaveProjectAsync -> top toast`: present in both docs
  - runtime query single-entry (`RuntimeQueryUseCase.ExecuteAsync`): present in both docs
- Conclusion: no immediate stale-doc conflict found in sampled contracts.

## Recommended S7 backlog seed
1. S7.1 Full-repo line-ending normalization + `lint.ps1 -AllFiles` pass.
2. S7.2 `UiLayoutGuardTests` stale assertions cleanup and re-enable `ui-snapshots` gate in regular milestone flow.
3. S7.3 Hotspot split: `NumberScrubber.axaml.cs` (~592 lines).
4. S7.4 Hotspot split: `FreeformHelperViewModel.Operations.LayerSelection.cs` (~591 lines).
5. S7.5 Hotspot split: `NotchTableGenerator.Generation.cs` (~574 lines) with behavior lock tests.
