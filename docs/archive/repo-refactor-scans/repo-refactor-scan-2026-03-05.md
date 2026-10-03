# Repo Refactor Scan

Generated: 2026-03-05 08:08:53
Branch: master

## File counts
- tracked files: 452
- src: 327
- tests: 67
- docs: 26
- scripts: 19

## Line count hotspots
- src\FreeformHelper.UI\Views\DevView.axaml: 1245
- src\FreeformHelper.UI\ViewModels\FreeformHelperViewModel.NotchDetails.cs: 866
- src\FreeformHelper.Application\Services\NotchTableGenerator.cs: 864
- src\FreeformHelper.UI\Controls\PadCanvas.Input.cs: 854
- src\FreeformHelper.UI\Views\FreeformHelperView.Canvas.cs: 804
- src\FreeformHelper.UI\Controls\PadCanvas.cs: 798
- src\FreeformHelper.UI\Controls\PadCanvas.Rendering.cs: 797
- src\FreeformHelper.UI\ViewModels\FreeformHelperViewModel.State.cs: 794
- src\FreeformHelper.UI\ViewModels\FreeformHelperViewModel.Settings.cs: 726
- tests\FreeformHelper.Tests\UI\ViewModels\FreeformHelperViewModelTests.Basics.cs: 722
- src\FreeformHelper.UI\Services\ManualSizingService.cs: 713
- src\FreeformHelper.UI\Controls\PadCanvas.View.cs: 712
- src\FreeformHelper.UI\Services\RuntimeQueryUseCase.Commands.cs: 693
- src\FreeformHelper.Application\Services\DxfVisibleIndexAssignmentService.cs: 686
- tests\FreeformHelper.Tests\UI\ViewModels\FreeformHelperViewModelTests.SettingsPersistence.cs: 678
- src\FreeformHelper.UI\ViewModels\FreeformHelperViewModel.Selection.cs: 671
- src\FreeformHelper.Application\Services\NotchV22CompensationService.cs: 652
- src\FreeformHelper.Application\Services\DxfOverlapAnalyzer.cs: 650
- src\FreeformHelper.UI\Views\FreeformHelperView.axaml.cs: 631
- src\FreeformHelper.UI\ViewModels\FreeformHelperViewModel.DxfEditing.cs: 631
- src\FreeformHelper.UI\Views\FreeformHelperView.Console.cs: 616
- src\FreeformHelper.UI\Styles\Controls.Form.axaml: 596
- src\FreeformHelper.UI\ViewModels\NotchExportSelectionViewModel.cs: 595
- src\FreeformHelper.UI\Controls\NumberScrubber.axaml.cs: 592
- src\FreeformHelper.UI\ViewModels\FreeformHelperViewModel.Operations.LayerSelection.cs: 591

## Analyzer summary
- command: `dotnet build FreeformHelper.sln -c Debug /p:UseAppHost=false /nologo`
- result: `NO_WARNINGS`
- log: `build/logs/repo-scan-build-2026-03-05.log`

## Lint status (all files)
- command: `./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost`
- result: `FAIL`
- root causes:
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.PadInspector.Snapshots.cs`: `FINALNEWLINE`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.PadInspector.Formatting.cs`: `IMPORTS`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.PadInspector.Trace.cs`: `IMPORTS`

## Contract drift check
- checked files:
  - `docs/reference/behavior-inventory.md`
  - `docs/guides/settings-entry-matrix.md`
  - `AGENTS.md`
- status: no drift found for current runtime contracts:
  - app settings deferred persistence after `Load Project`
  - `Ctrl+S` -> `SaveProjectAsync()` + top toast
  - terminal `AvaloniaEdit TextEditor` single-path + default expanded
  - runtime query CLI routed through `RuntimeQueryUseCase`

## New optimization candidates
1. `P0` Recover `lint -AllFiles` baseline (format/import ordering regression).
2. `P1` Split `src/FreeformHelper.UI/Views/DevView.axaml` (1245 lines) into section views.
3. `P1` Split `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchDetails.cs` (866 lines) into snapshot/trace/export helpers.
4. `P1` Split `src/FreeformHelper.UI/Controls/PadCanvas.Input.cs` (854 lines) into pointer/keyboard/selection partials.
5. `P1` Split test hotspot `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.Basics.cs` (722 lines) into focused suites.
