# UI Density And Token Rules

Status: active for beta0.10 UI work.

## Purpose

FreeformHelper has three different UI densities. A view must choose one density up front instead of tuning spacing locally.

- `normal`: full windows and review surfaces.
- `compact`: settings cards, inspector sections, and export review side panes.
- `narrow`: right workflow panel, pad popovers, and any panel below roughly 360 px width.

## Density Rules

### Normal

Use for large surfaces where horizontal scanning is acceptable.

- Windows: `SettingsWindow`, export review, report windows.
- Layout: two or three column grids are allowed.
- Copy: short section description is allowed, but detailed behavior belongs in tooltip or docs.
- Cards: use section-level blocks, then field tiles inside.

### Compact

Use for secondary review panes and dense settings groups.

- Prefer card stacks over long paragraphs.
- Use one label and one value per tile.
- Use `workflowStepStatusStrip`, `workflowStepMetricTile`, `settingsFieldTile`, or existing inspector card styles.
- Keep dynamic text wrapping explicit or covered by a style guard.

### Narrow

Use for the right workflow panel and pad info popovers.

- One primary action per step/card.
- First layer shows status, next action, and the current effective settings.
- Advanced/debug/trace sections default collapsed.
- Avoid side-by-side content unless each column still has readable labels.
- If a button label can clip, shorten it or use an icon plus tooltip.

## Token Rules

- No inline colors in Views/Controls.
- New color, radius, spacing, size, or opacity values go through `Tokens.axaml`.
- Views consume styles/classes first; only use direct `StaticResource`/`DynamicResource` when a class cannot express the layout.
- Disabled, hover, focus, and selected states must come from shared control styles.
- Do not create one-off card/background styles inside a View.

## Current Shared UI Contracts

- Action role source of truth: `docs/guides/ui-action-role-system.md`.
- Action controls: choose one base primitive and one semantic role from the Action Role System before adding or changing XAML classes.
- Settings common fields: `settingsFieldTile`, `panelFormField`, `SettingsInfoLabel`.
- Workflow status cards: `workflowStepStatusStrip`, `workflowStepMetricTile`, `workflowStepMetricLabel`, `workflowStepMetricText`.
- Workflow sections: `workflowStepSubsection`, `workflowStepInsetPanel`.
- Summary badges: passive badges use `chipStatus`; interactive badges use `actionChip chipAction`.
- Workspace file rows: `workspaceFileRow`, `workspaceFileRowTitle`, `workspaceFileRowMeta`, `workspaceFileRowIcon`.
- Pad info relation cards: `PadInfoRelationCardListView`.
- Low-frequency pad info: `padInfoSectionToggle debugCard` with details hidden behind `ShowGeometryDetails`.

## Review Checklist

Before committing a UI change:

- Confirm the view has a declared density: normal, compact, or narrow.
- Confirm every button, toggle, clickable chip, and passive badge maps to exactly one documented Action Role System semantic role.
- Check that action controls do not mix forbidden role combinations such as `consoleHeaderAction icon`, `viewportOverlayAction panelAction`, or ordinary checks using `workspacePrimaryAction`.
- For style/token changes, preview the `DevView` action role laboratory and run the checklist in `docs/guides/ui-action-role-visual-qa.md`.
- Check that dynamic text wraps/trims or is covered by a style guard.
- Check that advanced/debug/trace content is not expanded by default in narrow panels.
- Run `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj /p:UseAppHost=false`.
- Run `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~UiLayoutGuardTests" /p:UseAppHost=false` when XAML structure or style rules change.
- Run `./scripts/tests/lint.ps1 -UseNoAppHost` before commit.

