---
name: ui-consistency
description: Use for FreeformHelper UI layout/style consistency work. Trigger when editing Views/Styles, spacing, tokens, icon affordances, or panel/form alignment to prevent spacing-stacking and clipping regressions.
---

# Skill: FreeformHelper UI Consistency

## Purpose
Prevent layout drift and visual regressions in workspace/settings panels.

## When to use
- Any change in `src/FreeformHelper.UI/Views/*.axaml`
- Any change in `src/FreeformHelper.UI/Styles/*.axaml`
- Any update to input controls, panel spacing, icon affordance, or scroll layout

## Layout contract (must follow)
1) Single spacing owner
   - Use container spacing as the primary gap control.
   - Do not combine container spacing + per-control vertical margins.
2) Two-column form rows
   - Use a two-column `Grid`.
   - Use `StackPanel.panelFormField.left/right` for inter-column gap.
   - Keep gap in tokens `InsetFormColumnLeft` / `InsetFormColumnRight`.
3) Label + input field group
   - Wrap with `StackPanel.panelFormField`.
   - Keep field internal spacing in token `PanelFormFieldSpacing`.
4) Token-first values
   - Any new size/thickness/radius/spacing goes to `Tokens.axaml`.
   - Views consume tokens/resources only.

## Verification checklist
- No new inline color/size literals in Views/Controls.
- New two-column groups use `panelFormField.left/right` (not inline margin).
- Label/input stacks use `panelFormField`.
- Scrollbar/slider behavior previewed in `DevView`.
- Run `./scripts/dev/prepare-ui-workspace.ps1` before build/test/lint to stop stale UI processes and normalize changed files to CRLF.
- Tooltip style changes must pass both static style collision guards and the headless tooltip-open smoke test; do not rely only on app startup.
- If build/test reports `FreeformHelper.UI` DLL locks, run `./scripts/dev/prepare-ui-workspace.ps1 -SkipNormalizeLineEndings` once before retrying.
- Run `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`.
