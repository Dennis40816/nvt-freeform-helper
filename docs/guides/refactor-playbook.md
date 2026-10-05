# FreeformHelper Refactor Playbook (for AI Agents)
Last updated: 2026-08-08

## Goals
This document provides a refactoring process that can be followed directly, so different AI agents/developers can maintain consistent quality during handoffs:
- Consistent behavior (single-entry, explicit side effects)
- Traceable commits (small steps, commits with a single logical scope)
- Reproducible verification (fixed build/test/baseline process)

## Entry points and prerequisite documents
1. `TODO.md`
2. `docs/README.md`
3. `docs/core/workflow-pipeline.md`
4. `docs/core/notch-validation-flow.md` (when changing Step6 validation/Notch rows)
5. `.agents/skills/freeform-refactor/SKILL.md`
6. `.agents/skills/ui-consistency/SKILL.md`
7. `docs/guides/ui-density-token-rules.md` (when changing UI layout/style)

## Automatic Skill discovery location in the repo
- Codex scans `.agents/skills/<skill-name>/SKILL.md` in the repo.

## Branch and commit workflow
1. **Check the branch first**: avoid editing directly on `master/main`.
2. **One commit per milestone**: do not mix in unrelated fixes.
3. At every milestone, run:
   - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
   - Necessary targeted tests (such as workflow/snapshot/export)
4. Push only after they pass.
5. Before a PR, apply:
   - `docs/guides/refactor-pr-checklist.md`
   - The latest repo scan must be checkable against `docs/guides/repo-refactor-scan-template.md`

## Standard refactoring cycle (the same for every iteration)
1. **Inventory**
   - Identify the current multiple entry points: Command, PropertyChanged, View event, CLI.
2. **Unique Path Audit**
   - Distinguish "multiple entry points that ultimately converge on the same path" from "the same result being recalculated independently in multiple places".
   - The former is acceptable; the latter must be recorded as a defect or refactoring item.
3. **Single Entry / Single Result design**
   - Decide on the single entry point (usually in `UseCase` or `UI/Services`).
   - Decide on the single result model: the same user-visible result can have only one source-of-truth model.
4. **Document Side Effects**
   - Explicitly list: selection clear, downstream invalidation, focus/step jump, status text.
5. **Implementation**
   - Keep only UI state/binding in the ViewModel; move logic to a service/usecase.
6. **Verification**
   - Build + targeted tests.
7. **Synchronize documentation**
   - Update `TODO.md` status and the necessary spec/README entry points.
8. **Commit**
   - `type(scope): subject` + a clear body (what changed and why).

## Single Entry vs Single Result (must be checked separately)
- `Single Entry`: the same user action should come through a single entry point, such as `SaveProjectAsync()` or `RuntimeQueryUseCase`.
- `Single Result`: the same user-visible result should be produced by only one core calculation path; other places may only read/project it, not derive it again.
- Acceptable:
  - Multiple buttons/shortcuts call the same command.
  - Multiple UI panels read the same snapshot/result model.
- Unacceptable:
  - UI / RuntimeQuery / export / inspector independently recalculate `ToFull enabled`, `Stage3Area`, and `CombinedRatio` from partial data.
  - Multiple formatters / ViewModels independently compose the same set of business conclusion strings, causing display drift.

## Unique Path scan checklist (required for repo scans)
1. Identify all entry points and all readers for the same feature/result.
2. Check for second-pass derivation:
   - UI recalculates independently
   - RuntimeQuery recalculates independently
   - export recalculates independently
   - inspector/pad info reconstructs it again from partial data
3. If second-pass derivation exists:
   - First record it in `TODO.md`
   - Then define a single source-of-truth model
   - Finally add regression tests to lock in consistency across readers.

## Direct State Mutation scan checklist (required for repo scans)
1. Identify public mutable collections / state models that can be mutated directly from outside.
2. Mark a convergence route for each hotspot:
   - private mutable backing + read-only facade
   - clone-on-set
   - dedicated replace/apply API
3. If not addressed in this iteration, list the owner + target milestone in the scan report.
4. Before a PR, check off every item in the S11.52 gate in `docs/guides/refactor-pr-checklist.md`.

## Equivalence of data structure changes (mandatory)
- Every data structure adjustment (field renaming/typing/model splitting) must include proof that results are unchanged before and after.
- Minimum requirements:
  - For the same inputs, output `NotchTableRow.Values` stays identical (or follows explicit migration rules).
  - Export results (CSV/C initializer) are equivalent before and after.
  - Validation / Runtime Query user-visible results are equivalent before and after.
- Recommended approach:
  - Add a set of typed vs legacy equivalence tests (same inputs, same ordering, same results).
  - If a fallback path exists, tests must cover both primary + fallback.

## Project rules (key points)
### Architecture
- Pure logic: `src/FreeformHelper.Application`
- UI orchestration: `src/FreeformHelper.UI/Services`
- ViewModel: keep only state and command bindings to avoid scattered business logic

### UI/styles
- No inline color/size; use `src/FreeformHelper.UI/Styles/Tokens.axaml`
- New resources must be theme-aware (`DynamicResource`)
- Icon sizes/brushes follow shared conventions, with no ad-hoc choices

### Line endings/file format (mandatory)
- Repo text files use `CRLF` as the standard, following `.editorconfig` / `.gitattributes`.
- After editing with a script, `apply_patch`, or an external tool, if touched files have `ENDOFLINE` issues, normalize that batch of files back to `CRLF` before committing.
- Do not leave line-ending fixes to a later lint stage; this makes analyzer/build gates fail for non-logic issues.

### Workflow (Step1~Step5)
- Centralize step dependencies, invalidations, and focus rules in the pipeline service
- Do not hand-code the same "Invalidate + Move" logic in multiple files

### Settings hierarchy (mandatory)
- Always follow: `Project (full settings snapshot) > App General (whitelist) > Default (program defaults)`
- `Project Save/Load` retains the full `ProjectSettings + ProjectUiSnapshot` roundtrip; do not trim it.
- `App General` stores only the whitelist:
  - `UiViewSnapshot`
  - `UiImportSnapshot`
  - `Behavior.ApplyVisualPreferencesOnProjectLoad`
- `Load project` behavior:
  - Geometry/workflow settings always follow the project.
  - `ApplyVisualPreferencesOnProjectLoad` determines whether the visual layer is overridden (ON by default).
  - After loading, app-level writes enter deferred mode and flush only on the next successful `Save Project` (to avoid overwriting app settings along the way).

### Settings entry responsibilities (M11 convergence)
- `WorkspaceHeader` Display popup: only frequent, immediate visual adjustments (direct TwoWay).
- `SettingsWindow`: entry point for required operator-facing settings (General + Step1~5), managed through draft/apply; a full persistence schema does not mean every field must have a normal-flow editor.
- `RightWorkflowPanel`: workflow actions + General shortcuts + deep link.
  - `General Settings` keeps common fields directly editable (Grid / AA / source / alignment).
  - Step sections retain only manual decisions and necessary shortcuts actually required for normal operation (such as Step3 stage/autoplay); automatic policy, calibration/scoring, derived, diagnostic, and compatibility fields must not be put into the numbered flow for schema completeness.
- `CoordinatePixelWidth/Height` and Step4 mapping weights/candidate/confidence/ambiguous thresholds are outside the normal operator flow; when migrating the UI, hide/separate them first, preserve defaults, consumers, and project roundtrip, and do not delete schema in the same slice.
- Maintain the entry matrix and single-source mapping centrally in `docs/guides/settings-entry-matrix.md`.

## Recommended test categories (execution order)
1. **Fast logic tests** (Application/Workflow)
   - Run first for the fastest feedback
2. **ViewModel/Service integration tests**
   - Verify workflow side effects
3. **UI Snapshot tests**
   - Update baselines only when UI structure/styles change

Recommended commands (examples):
- `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug --filter FullyQualifiedName~WorkflowPipelineServiceTests`
- `./scripts/tests/update-ui-baseline.ps1 -Mode DryRun`
- `./scripts/tests/run-refactor-gate.ps1 -UseNoAppHost` (always runs all test groups, including `ui-snapshots`)

## Common mistakes and safeguards
1. **Inconsistent behavior after refactoring**
   - Cause: entry points have not converged, and old paths still bypass the new service.
   - Safeguard: use `rg` to find old helper call sites and change each to the new entry point.
2. **UI looks normal but rules drift**
   - Cause: CLI/right-side buttons/shortcuts use different logic.
   - Safeguard: make UI and CLI share the same method.
3. **Snapshot test contamination**
   - Cause: DryRun/Apply were not distinguished, or unrelated UI changes were mixed in.
   - Safeguard: run `DryRun` first, confirm differences cover only the intended change, then run `Apply`.

## Handoff template (paste into PR or commit description)
1. Goal of this iteration:
2. Single entry point:
3. Side effect changes:
4. Verification commands and results:
5. TODO status synchronization:
6. Recommendations for the next iteration:

