# Non-Notch Optimization Baseline
Last updated: 2026-02-20

## 1) Current snapshot (measured)
- Source files:
  - `src` C#: 171
  - `src` AXML: 20
  - UI C# files: 105
- Tests:
  - Test C# files: 29
  - Total tests passing: 101
- Large hotspots (by lines):
  - `src/FreeformHelper.UI/Views/FreeformHelperView.axaml`: 939
  - `src/FreeformHelper.UI/Controls/PadCanvas.Input.cs`: 836
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Operations.cs`: 820
  - `src/FreeformHelper.UI/Services/ManualSizingService.cs`: 809
  - `src/FreeformHelper.UI/Styles/Controls.axaml`: 806
- Rebuild triggers:
  - `TriggerGridRebuildAsync(...)` callsites: 38
  - `requestFit:true`: 25
- Logging signal/noise (sample from `build/logs/app.log`):
  - Total lines: 547
  - `Selection updated`: 179
  - `Grid built`: 55
  - `ViewModel initialized`: 115
- Open TODO items:
  - Total open: 15
  - Non-Notch: 10
- Startup/load baseline (`scripts/perf/measure-startup-load-batch.ps1`, `-Runs 2`, project `example/BOE36.35/project_3635.json`):
  - Startup ready: cold=1683 ms, warm(avg)=1706 ms
  - Load project total: cold=846 ms, warm(avg)=895 ms
  - Load split (latest run): persistence=126 ms, apply=17 ms, dxf=180 ms, rebuild=41 ms, step1=240 ms, step2=23 ms

## 2) Optimization opportunities (excluding notch algorithm)

### A. Performance
P0
- Canvas draw pipeline: currently loops full pad sets multiple times per frame; add viewport prefilter + single-pass classification list to reduce CPU and GC.
- Progress/update pipeline: `Match / DXF overlap / Index diagnostics` each has duplicated progress-throttle logic; extract shared helper to reduce UI thread churn and code duplication.
- Rebuild scheduling: keep single-entry policy, add explicit coalescing metrics and optional debounce window for setting bursts.

P1
- Low-zoom decimation: skip labels/hatches for sub-pixel pads and only render coarse geometry when zoomed out.
- Incremental cache invalidation: avoid rebuilding area/color/geometry cache when unrelated settings change.

P2
- Spatial acceleration for high-volume overlap checks (tile index / bbox buckets before polygon intersection).

### B. UX / Flow
P0
- Settings IA cleanup: keep right panel strictly as Step 1~4 workflow; move low-frequency toggles to Settings window.
- Naming consistency: unify `CAD idx / CAD id / Regular id / Diff idx` terminology in UI and docs.
- Selection clarity: add explicit selection mode hint (Normal vs Alt-regular-priority) near canvas tools.

P1
- Focus/Locate interaction unification: same behavior for report locate, match locate, and overlap locate (with minimum zoom and status text).
- Diagnostics readability: per-type filters + concise summary + copy action in reports.

### C. Debuggability / Observability
P0
- Log level policy:
  - Selection spam to `Debug`.
  - Keep state transitions (`build/match/save/load`) at `Info`.
  - Unexpected fallback/invalid state at `Warn`.
- Add operation timing logs for: grid rebuild, match, overlap check, diagnostics.

P1
- In-app diagnostics panel:
  - visible CAD/regular counts
  - render ms (avg/p95)
  - rebuild duration
  - current zoom/pan
- Diagnostic bundle export (settings + summary + report excerpts) for bug reports.

### D. Codebase reduction / structure / naming
P0
- Extract shared progress runner (one helper for async operation + progress posting + status).
- Keep backward compatibility aliases, but rename source file/term drift:
  - `AfeMapper.cs` -> `DiffIndexMapper.cs` (alias kept as deprecated wrapper).
  - remove AFE wording from non-hardware-facing UI copy.

P1
- Split oversized UI file:
  - `FreeformHelperView.axaml` into `LeftPanel/RightPanel/CanvasOverlay`.
- Split VM hot files into UseCase-oriented services where logic is not UI-state-only.

P2
- Introduce explicit domain key type for mapping/selection contexts (e.g., `(IcIndex, DiffIndex)` value object) to reduce ad-hoc tuple/string usage.

## 3) Proposed implementation order

### Sprint 0 (recommended next)
1. Canvas viewport prefilter + draw classification pass.
2. Shared progress runner for three long-running actions.
3. Log policy normalization and downgrade noisy selection logs.
4. Naming cleanup pass (UI copy + docs + deprecated alias annotations).

### Sprint 1
1. Right panel/settings information architecture cleanup.
2. Locate/focus interaction unification.
3. File/module split for `FreeformHelperView.axaml` and VM hotspots.

## 4) Acceptance checks
- Build: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
- Tests: `dotnet test FreeformHelper.sln --no-build`
- Baseline: `./scripts/perf/collect-perf-baseline.ps1`（輸出：`build/perf/non-notch-perf-baseline-latest.md`）
- No behavior regressions on:
  - DXF import/layer filtering
  - overlap check + progress
  - match + progress
  - diagnostics locate
  - save/load roundtrip

