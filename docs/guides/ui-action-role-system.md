# UI Action Role System

Status: source of truth for S12.025 and later Action Role System migration slices.

## Purpose

This guide defines the single architecture for FreeformHelper action controls without forcing every control to look identical.

The target is a role-based system:

- Tokens define colors, radius, spacing, size, and contrast.
- Base primitives define shared Button / ToggleButton behavior.
- Semantic roles define where a control lives and what it means.
- Lint prevents invalid class combinations from returning.

## Scope And Authority

This document is the canonical contract for buttons, toggles, clickable chips, and passive badges across the UI. During S12.026-S12.029, implementation work should update this guide first when a role contract changes, then update styles, Dev page previews, migration code, and lint guards to match it.

The contract has three decisions for every action-like control:

- Density: `normal`, `compact`, or `narrow`, as defined in `docs/guides/ui-density-token-rules.md`.
- Primitive: the mechanical layout class, such as `actionIconButton` or `actionTextButton`.
- Semantic role: exactly one role class, such as `actionNeutral` or `viewportOverlayAction`.

If a control cannot choose one semantic role from this guide, treat that as a missing role and update this document before adding more XAML classes.

## Current Root Causes

The recent sidebar and console fixes exposed the same underlying problems:

- One control can stack multiple strong classes, such as `icon`, `panelAction`, `workspacePrimaryAction`, and a domain-specific class. Avalonia selector specificity and include order then decide the final state style.
- Button and ToggleButton have different state contracts. ToggleButton needs `checked`, `checked:pointerover`, and `checked:pressed`; missing those states creates inconsistent selected hover behavior.
- `FontIcon` foreground does not reliably follow the parent foreground unless the role style explicitly binds or overrides it.
- Legacy names are too broad. `panelToggle` previously described both side-pane chrome controls and canvas overlay controls.
- Clickable chips, status badges, and presets used similar pill visuals, so users could not easily tell which elements are interactive.

## Architecture

### Layer 1: Tokens

Tokens live in `src/FreeformHelper.UI/Styles/Tokens.axaml`.

Required token groups:

- Neutral action: background, hover, pressed, border, foreground.
- Primary action: bright/accent action surface, visible border, hover, pressed, high-contrast foreground.
- Danger action: dark action surface, danger-tinted border, hover, pressed, danger foreground.
- Focus action: non-accent focus background, border, and foreground for keyboard-visible focus.
- Inverse action: hover background, pressed background, foreground.
- Neutral selected: checked background, checked border, checked foreground, without accent-blue borders.
- Status chip: passive background, border, text foreground.
- Action chip: interactive background, hover, pressed, border, text foreground.
- Icon control: size, icon size, border thickness, radius.

Rules:

- Views and controls must not introduce inline colors or sizes.
- If a state needs a color that is not represented by a token, add a token first.
- Do not use bare `BrushWhite` / `BrushBlack` as a semantic hover contract. Use a role token, such as `BrushActionInverseForeground`.

### Layer 2: Base Primitives

Base primitives should define mechanical behavior only:

- Fixed dimensions for icon-only buttons.
- Text clipping / no-wrap / ellipsis for compact text buttons.
- Horizontal and vertical content alignment.
- `ClipToBounds` for all compact actions.
- Shared icon foreground inheritance pattern.

Candidate primitive classes:

- `actionButton`
- `actionIconButton`
- `actionTextButton`
- `actionToggleButton`
- `actionChip`

These primitive names are final for the migration unless a later TODO explicitly changes this guide first. Primitives should not encode workflow meaning. They are allowed to define structure and safe layout only.

### Layer 3: Semantic Roles

Semantic roles define user meaning and state colors.

| Role | Primitive | Control types | Intended use | Required visual behavior | Typical current mapping |
| --- | --- | --- | --- | --- | --- |
| `actionNeutral` | `actionTextButton` or `actionButton` | `Button`, `ToggleButton` when needed | Ordinary sidebar or form action | Dark surface, muted border, light foreground, visible hover/pressed/focus feedback, no accent border | DXF Overlap check, Quick Check, Start/End helpers |
| `actionPrimary` | `actionTextButton` or `actionButton` | `Button` primarily; `ToggleButton` only for selected workflow modes | True forward workflow action | Brighter/accent surface reserved for actions that advance or commit the workflow | Run, Import, Add point/path when it advances the flow |
| `actionDanger` | `actionTextButton` or `actionButton` | `Button` | Destructive or reset action | Dark surface with danger foreground/border; readable hover/pressed/focus states without a full red fill | Clear all, Clear CSV, Clear custom |
| `actionGhost` | `actionIconButton` or `actionTextButton` | `Button`, `ToggleButton` when needed | Low-emphasis action in headers or dense rows | Minimal chrome by default; visible neutral hover/pressed feedback | Settings, edit, delete, dense-row helpers |
| `viewportOverlayAction` | `actionIconButton` | `Button`, `ToggleButton` | Canvas overlay icon/toggle | Inverse/overlay-safe surface; checked state must remain readable above canvas content | Simulation Fit AA, Coordinate Fit AA, show/hide overlay layers |
| `panelChromeToggle` | `actionIconButton` | `Button`, `ToggleButton` when needed | Side-pane show/hide chrome | Compact fixed-size chrome; neutral hover/pressed states | Hide left/right panel |
| `consoleHeaderAction` | `actionIconButton` or compact `actionTextButton` | `Button`, `ToggleButton` | Terminal toolbar action/toggle | Console header density; checked state distinct from hover | Dedup, copy, clear, jump, font size |
| `chipAction` | `actionChip` | `Button`, `ToggleButton` when needed | Clickable preset/filter chip | Pill-like interactive states; must show hover/pressed and optional checked | Uniform preset, selectable filter chips |
| `chipStatus` | none; passive chip style | `Border`, `TextBlock`, `SelectableTextBlock` containers only | Passive status/badge | No hover/pressed contract; status severity colors allowed through tokens | EMS OK, Violations 0, allocation summary |
| `workspaceFoldHeader` | dedicated fold header style | `ToggleButton` or existing fold header control | Collapsible second-level diagnostic section | Header/body accordion behavior; must not look like old dropdown panel | EMS safety fold header |

Rules:

- A control must have one semantic role.
- A control may combine one base primitive and one semantic role.
- A control must not combine two semantic roles.
- Domain-specific classes may style layout, but must not override action state colors unless they are declared as a semantic role.

## Class Composition Contract

Use this contract when writing or reviewing XAML:

- `Classes="{primitive} {role}"` is the preferred final form for action controls.
- `icon`, `iconTextButton`, `panelAction`, `workspaceActionButton`, `simulationActionButton`, and other legacy layout helpers are migration aliases only. S12.026 may temporarily bridge them, but new XAML should use the final primitives and roles.
- Domain-specific classes may only set layout that is local to the view, such as row placement or visibility. They must not set `Background`, `BorderBrush`, `Foreground`, `Padding`, `MinWidth`, `Width`, `Height`, `CornerRadius`, or icon foreground for an action control.
- `chipStatus` is passive and must not be used on `Button` or `ToggleButton`.
- `chipAction` is interactive and must be used on a control with `Command=`, `Click=`, or a documented selection binding.
- `workspacePrimaryAction` is a legacy primary role alias. During migration it maps to `actionPrimary`; after migration, ordinary checks must not use it.
- `panelToggle` is deprecated. Use `panelChromeToggle` for side-pane chrome or `viewportOverlayAction` for canvas overlays.

## State Contract

Every action role must explicitly cover this matrix.

| State | Button | ToggleButton |
| --- | --- | --- |
| default | required | required |
| pointerover | required | required |
| pressed | required | required |
| disabled | required | required |
| checked | not applicable | required |
| checked:pointerover | not applicable | required |
| checked:pressed | not applicable | required |
| icon foreground | required | required |

Text and icon contrast must stay readable in every state. For icon controls, the icon foreground must either bind to the parent foreground or use a role-specific selector for each state.

State implementation requirements:

- Every role defines `Background`, `BorderBrush`, `Foreground`, and any role-specific `Opacity` for default, pointerover, pressed, and disabled.
- Toggle roles additionally define checked, checked:pointerover, and checked:pressed. Checked hover must not fall back to the unchecked hover color.
- Shared action focus uses role tokens instead of the platform accent adorner, so keyboard focus remains visible without introducing blue borders.
- Icon-only primitives define fixed `Width`, `MinWidth`, `Height`, `Padding`, `CornerRadius`, and `ClipToBounds`.
- Icon foreground must be handled inside the role selectors or by an explicit parent foreground binding pattern. Do not rely on accidental `FontIcon` inheritance.
- Disabled state must remain visually disabled without losing the role's size contract.

## Allowed Mapping Examples

| Control | Target role |
| --- | --- |
| DXF Overlap check / Quick Check | `actionTextButton actionNeutral` |
| Simulation Fit AA | `actionIconButton viewportOverlayAction` |
| Coordinate Fit AA | `actionIconButton viewportOverlayAction` |
| Console Dedup toggle | `actionIconButton consoleHeaderAction` or compact `actionTextButton consoleHeaderAction` |
| Console collapse toggle | `actionIconButton panelChromeToggle` or dedicated console chrome role if it diverges |
| Uniform 400 / Uniform 360 / Zero presets | `actionChip chipAction` |
| EMS OK / Violations 0 / Max After 0 | `chipStatus` |
| EMS safety details | `workspaceFoldHeader` + `workspaceFoldBody` |

## Required Decision Table

These controls are named because they were the regression-prone cases that started the Action Role System work.

| Control | Final role decision | Notes |
| --- | --- | --- |
| Quick Check / Overlap Check | `actionTextButton actionNeutral` | Ordinary review/check commands, not primary workflow actions. |
| Simulation Fit AA | `actionIconButton viewportOverlayAction` | Canvas overlay icon; must not combine with panel action classes. |
| Coordinate Fit AA | `actionIconButton viewportOverlayAction` | Same overlay role as Simulation Fit AA. |
| Console Dedup | `actionIconButton consoleHeaderAction` or compact `actionTextButton consoleHeaderAction` | Header toolbar toggle; checked:hover must stay console-specific. |
| Uniform/Zero presets | `actionChip chipAction` | Interactive preset selection, distinct from passive status badges. |
| EMS OK / Violations / Max After badges | `chipStatus` | Passive status; no command or click binding. |

## Forbidden Combinations

These combinations should fail lint after the guard is implemented:

- `consoleHeaderAction icon`
- `viewportOverlayAction panelAction`
- `viewportOverlayAction panelChromeToggle`
- `panelChromeToggle panelAction`
- `chipStatus` with `Command=` or `Click=`
- `chipAction` without interactive hover/pressed style
- `workspacePrimaryAction` on ordinary check/review commands
- `panelToggle` in new XAML

S12.028 lint must report file path and line number for these combinations. Before S12.028 lands, reviewers should block these combinations manually.

## Migration Compatibility

S12.026 may keep compatibility aliases while styles are being split, but each alias needs a documented migration target:

| Legacy class or pattern | Migration target |
| --- | --- |
| `panelAction` ordinary text button | `actionTextButton actionNeutral` |
| `panelAction icon` ordinary icon button | `actionIconButton actionGhost` |
| `panelAction workspacePrimaryAction` | `actionTextButton actionPrimary` or `actionIconButton actionPrimary` when truly primary |
| `panelAction workspaceDangerAction` | `actionTextButton actionDanger` |
| `workspacePrimaryAction` without `panelAction` | `actionTextButton actionPrimary` only when documented as workflow-primary |
| `viewportOverlayAction icon` | `actionIconButton viewportOverlayAction` |
| `coordinateOverlayAction` | `viewportOverlayAction` |
| `consoleHeaderAction icon` | `actionIconButton consoleHeaderAction` |
| `workspaceSummaryChipButton` | `actionChip chipAction` |
| `workspaceSummaryChip` passive border | `chipStatus` |
| `panelToggle` | `panelChromeToggle` |

## Implementation Slices

### S12.025: Role Matrix And Contract

Deliverables:

- Keep this guide as the source of truth.
- Add role references to `docs/guides/ui-density-token-rules.md`.
- Decide final class names before style migration begins.

Acceptance:

- Every common action type has exactly one documented role.
- Quick Check, Fit AA, Dedup, presets, and badges are explicitly mapped.

### S12.026: Shared Style Primitives

Deliverables:

- Add `src/FreeformHelper.UI/Styles/Controls.Action.axaml`, or equivalent action-only style include.
- Move shared action state contracts into this file.
- Keep existing files as compatibility wrappers while migration is underway.
- Add missing tokens to `Tokens.axaml`.

Acceptance:

- Dev page can preview neutral, primary, danger, ghost, viewport, console, action chip, and status chip states.
- Button and ToggleButton variants both cover the full state matrix.
- No inline color/size values are added to Views or Controls.

### S12.027: Repo Migration

Deliverables:

- Scan all `.axaml` files for Button, ToggleButton, preset, chip, and badge classes.
- Migrate Freeform, Simulation, and Coordinate first.
- Migrate Console, Workflow steps, DXF edit, PadInfo, and Dev page second.
- Deprecate broad aliases, especially `panelToggle`.

Acceptance:

- `rg` finds no forbidden class combinations.
- Canvas overlay controls share `viewportOverlayAction`.
- Side-pane chrome controls share `panelChromeToggle`.
- Clickable chips and passive badges are visually distinct.

### S12.028: XAML Role Lint

Deliverables:

- Add a PowerShell guard such as `scripts/tests/check-xaml-action-roles.ps1`.
- Integrate it into `scripts/tests/lint.ps1 -UseNoAppHost`.
- Report file path, line number, and invalid class combination.

Acceptance:

- Adding a forbidden combination causes lint to fail.
- Existing valid role combinations pass without suppressions.

### S12.029: Dev Page And Visual QA

Deliverables:

- Add a Dev page role laboratory section.
- Show Button and ToggleButton variants for each role.
- Include disabled and checked states where applicable.
- Add a visual QA checklist for the known problem controls.

Acceptance:

- A token/style change can be reviewed in one Dev page location.
- Manual QA covers Console Dedup, terminal toolbar hover, Quick Check, Simulation Fit AA, Coordinate Fit AA, presets, and badges.

## Definition Of Done

The Action Role System is complete when:

- New controls choose one documented semantic role.
- Invalid class combinations are blocked by lint.
- Dev page previews every action role and state.
- Existing main surfaces use the same role names for the same user meaning.
- No user-facing button or toggle depends on selector order accidents for readable hover/pressed/checked states.
