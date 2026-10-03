# UI Action Role Visual QA

Status: baseline checklist for S12.029 and later action-role style changes.

## Scope

Use this checklist when changing action tokens, action role styles, or XAML classes on buttons, toggles, clickable chips, and passive status badges.

The primary preview surface is `DevView` section `Action role laboratory`. It must show:

- Neutral, primary, danger, and ghost text actions.
- Button, ToggleButton checked, icon-only, icon+text, and disabled variants.
- Viewport overlay icon actions, including checked and disabled states.
- Console header icon actions, including checked and disabled states.
- Chip action and chip status variants, including severity badges.
- Fold diagnostics, including the EMS safety corner count badge.

## Manual Checklist

- `DevView`: all action role rows remain readable in default, checked, and disabled states.
- Console terminal toolbar: Clear, Copy, Dedup, Jump, Search filter, and font controls keep consistent hover and checked behavior.
- Main workflow checks: Quick Check / Overlap Check read as neutral actions, not primary actions.
- Simulation viewport: Fit AA and viewport toggles read as overlay-safe icon actions.
- Coordinate viewport: Fit AA, regular grid, and label toggles match Simulation viewport behavior.
- Presets versus badges: Uniform/Zero preset chips read as clickable; EMS / violation / max-after badges read as passive status.
- EMS safety fold block: the violation count badge is visually light, centered on the block's top-right corner, and caps large counts at `99+`.
- Terminal toolbar hover: icon foreground tracks parent foreground without a separate glyph color override.

## Regression Signals

- A passive badge responds visually like a button.
- A clickable preset looks identical to a status badge.
- A checked ToggleButton falls back to the unchecked hover color.
- Viewport overlay icons look like side-panel chrome controls.
- Console toolbar icons use legacy `icon` class or diverge from shared icon sizing.
- The EMS safety count badge competes with the header label, sits inside the text flow, or renders counts above `99+`.
- Button text clips without ellipsis or wraps unexpectedly inside compact controls.

## Verification

Run the normal gates before committing:

- `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
- `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~UiLayoutGuardTests" /p:UseAppHost=false`
- `pwsh scripts/tests/lint.ps1 -UseNoAppHost`
