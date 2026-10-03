# Repo Refactor Scan - Draw Pipeline Rewrite (2026-03-08)

## Scope
- Branch: `feature/draw-pipeline-rewrite`
- Milestones covered:
  - `S11.1` frame render perf baseline
  - `S11.2` view refresh single-entry + frame snapshot
  - `S11.3` visible query / draw-list cache split
  - `S11.4` secondary visuals deferred during active navigation
  - `S11.5` frame model + release verification

## What changed
- `PadCanvasViewFrameSnapshot`
  - single source for viewport/world transform sampling
- visible draw cache split
  - query cache: viewport + CAD/Regular data revisions
  - draw-list cache: query revision + selection/highlight/resource + decimation
- secondary visuals moved off the navigation hot path
  - diff index overlay
  - notch overlays
  - match/notch ratio labels
  - axis labels
  - hover debug overlay
- render entry now resolves a single `PadCanvasRenderFrame`

## Validation
- `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj /p:UseAppHost=false`
- `scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
- `scripts/tests/run-tests.ps1 -Group smoke -UseNoAppHost`
- `scripts/tests/lint.ps1 -AllFiles -UseNoAppHost`

## Remaining non-blocking risks
- `visible query cache` is still single-slot; it now has clean layering but not multi-viewport reuse.
- geometry draw still owns the largest remaining cost on dense DXF projects.
- existing test-project `CA1861` warnings remain in untouched files and should be cleaned in a separate test-only pass.
