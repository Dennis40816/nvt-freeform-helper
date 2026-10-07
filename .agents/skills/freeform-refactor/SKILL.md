---
name: freeform-refactor
description: Use for FreeformHelper refactor and architecture changes. Trigger when tasks touch workflow steps, ViewModel behavior entry points, invalidation/focus rules, or UI style/token consistency in this repository. Do not use for pure domain math changes without UI/workflow impact.
---

# Skill: FreeformHelper Refactor Discipline

## Purpose
Provide a consistent workflow for refactoring behavior entry points and UI styling in this repository.
Primary reference: `docs/guides/refactor-playbook.md`.

## When to use
- Any refactor that touches user-visible behavior (commands, rebuilds, selection, zoom, workflow steps).
- Any UI change involving colors, typography, spacing, or icons in FreeformHelper.

## Workflow
1) Inventory behavior entry points
   - Commands: `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Commands.cs`
   - Property-change triggers: `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Settings.cs`
   - UI event handlers: `src/FreeformHelper.UI/Views/*.axaml.cs`
   - Canvas interactions: `src/FreeformHelper.UI/Controls/PadCanvas.*`
2) Decide the single entry
   - If multiple paths exist, propose a single UseCase/service entry.
   - Route ViewModel/Views to that entry; keep UI classes thin.
3) Confirm side effects
   - Explicitly list: selection clear, rebuild, fit/zoom, undo, status text, downstream invalidation.
4) Update tests
   - Prefer unit tests in `tests/FreeformHelper.Tests` for logic-only changes.
5) Commit discipline
   - Keep commits minimal and scoped; one logical change per commit with title + body.
   - Before build/test/lint, run `./scripts/dev/prepare-ui-workspace.ps1` to stop stale UI processes and normalize changed files to CRLF.
   - If a build/test retry is caused by `FreeformHelper.UI` DLL locks or CRLF warnings, use the prepare script once instead of repeating ad-hoc shell snippets.
   - After each logical stage, run `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj` and push the branch.
   - Avoid bundling unrelated fixes in the same commit.

## Style rules
- Add tokens in `src/FreeformHelper.UI/Styles/Tokens.axaml`.
- Use `DynamicResource` or `StaticResource` instead of inline literals.
- Avoid hardcoded colors/sizes/brushes in code-behind; resolve from resources.
- Icons must use shared size/brush tokens and consistent alignment.

## Deliverable checklist
- No new inline colors or sizes in Views/Controls.
- Any behavior change routed through a single entry.
- `ROADMAP.md` status synced: update the item's status and PR link; delete completed items once their release ships.
