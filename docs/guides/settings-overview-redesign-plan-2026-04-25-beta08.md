# Settings Overview Redesign Plan (Beta 0.8)

Date: 2026-04-25

## 1. Purpose

This plan records the UI changes needed after the beta0.8 notch allocation changes.

The key product decision is:

- Geometry area ratio remains the primary physical rule.
- `global 400` simulation is a diagnostic input, not proof of correctness.
- `After > 480` is treated as an EMS safety risk and must become visible before export.
- `ToFull` must be presented as `support / cap / boundary allowance`, not as direct target-weight expansion.

## 2. Current Problems

From the current workspace screenshots:

- The right panel mixes IC layout, grid settings, workflow actions, preview controls, and long help text in one scroll surface.
- Important settings are split between the inline right panel and `SettingsWindow`, so users cannot quickly tell which surface is authoritative.
- Step cards often say "managed in Settings > Step N" instead of showing the current effective value.
- Step 3 still reads like ToFull is a layer/preview feature, but beta0.8 changes its algorithm meaning.
- Simulation does not expose `EMS cap`, `maxAfter`, violation count, net-flow residual, or high-risk diff list.
- Step 5 export does not show whether the current table is safe under the simulation cap.

## 3. Design Direction

Use two clearly separated surfaces:

- Right workflow panel: operational overview and quick actions.
- Settings window: complete editable settings.

The right panel should answer:

- Is the project ready?
- Which algorithm/model is active?
- Is the current simulation safe under `After <= 480`?
- Which step needs action next?

The settings window should answer:

- Where do I edit each setting?
- What does this setting affect?
- Which values are currently active?

## 4. Right Workflow Panel Layout

### 4.1 Top Status Strip

Show compact chips:

- Project/grid status.
- Active IC layout summary.
- Compensation model, for example `No Gain`.
- Allocation model, for example `SourceArea support prototype`.
- Simulation safety, for example `Max After 452 / Cap 480`.

### 4.2 Workflow Quick Actions

Keep Step 1 to Step 6, but each card should be short:

- Title.
- One-line status.
- Primary action.
- Current effective settings as chips.
- Link to detailed settings.

Avoid long paragraphs inside the cards. Put details into tooltip, inspector, or SettingsWindow.

### 4.3 Algorithm Overview Card

Add a card that exposes the beta0.8 model:

- `ToRegular`: undo NF area flattening.
- `ToFull`: support / cap / boundary allowance.
- `Target allocation`: source-overlap dominant.
- `Gain`: off by default unless explicitly selected.

This prevents users from interpreting ToFull as a direct gain or direct full-area weight.

### 4.4 Simulation Safety Card

Add a card that shows:

- `EMS cap`: default `480`.
- `Max After`.
- violation count.
- top high-risk diffs.
- last simulation timestamp / source revision.
- action: `Open Simulation` or `Run safety audit`.

If `After > 480`, this card should be visually stronger than normal warnings.

## 5. Settings Window Layout

Replace the current tab-heavy layout with a left navigation rail and a single main content area.

Recommended navigation groups:

- Project / Grid.
- Geometry Match.
- Freeform Tagging.
- Notch Model.
- Simulation Safety.
- Export.
- Display / Debug.

Each page should use:

- section header with short description.
- status badges for effective values.
- two-column form rows for editable values.
- separate "advanced/debug" group at the bottom.
- sticky footer with `Save`, `Cancel`, `Reset`.

Implementation note (2026-04-25):

- `SettingsWindow` now uses a dedicated flat settings nav rail instead of reusing the shell capsule tab style.
- `Step 3` is split into cards for compensation model, signal semantics, allocation gates, and AA preview display.
- beta0.8 semantic badges use short text in the UI; longer explanations stay in docs or tooltips.
- The modern settings pass uses a dedicated settings color ramp and tile-based section layout so fields fill the content surface instead of floating in a narrow column.
- Settings explanatory copy now uses shorter operational text and a lower-noise note color so primary labels and input values stay dominant.
- Settings field help no longer renders a visible info badge. The field tile and label carry the hover tooltip directly, so the form stays visually quiet while each editable setting still exposes its explanation.

## 6. Missing Controls To Add

### 6.1 Notch Model

Expose:

- compensation model / profile.
- gain mode.
- allocation model.
- ToRegular effective state.
- ToFull semantic: support / cap / boundary allowance.
- strict overlap threshold.
- rule trace toggle.

### 6.2 Simulation Safety

Expose:

- EMS cap, default `480`.
- warning/fail policy for export.
- max-after audit result.
- net-flow residual report.
- high-risk diff report.

### 6.3 Export

Expose:

- v2.1 / v2.2 enabled state.
- `.c` export profile.
- CSV review export label.
- safety status at export time.

## 7. Page Sync Required By Beta0.8

### Step 3

Update Step 3 wording and status:

- show allocation model.
- show ToFull as support/cap/boundary allowance.
- show whether gain is active.
- show whether rule trace is active.
- remove wording that implies ToFull directly decides amount.

### Simulation

Add safety and trace fields:

- `EMS cap`.
- `maxAfter`.
- violation count.
- per-diff net-flow residual.
- high-risk diff list.
- source legs / target legs for selected diff.

### Step 5

Add export readiness:

- notch version/profile summary.
- safety audit status.
- explicit warning or block when `After > 480`.

### Inspector

Update tooltip fields:

- `Before`.
- `After`.
- `Delta`.
- `NetFlow`.
- `EMS status`.
- source/target leg summary when available.

## 8. Implementation Slices

- `S11.155`: beta0.8 page wording/status sync.
- `S11.156`: settings overview redesign planning and Dev page preview.
- `S11.157`: add simulation safety audit model and UI projection.
- `S11.158`: rebuild right workflow panel into overview + quick actions.
- `S11.159`: rebuild SettingsWindow navigation and form grouping.
- `S11.160`: add export-time safety status and policy.

Implementation note (2026-04-25):

- `S11.157` initial UI projection is implemented in Simulation details: `EMS cap`, `Max After`, violation count, top high-risk diffs, and selected-diff net-flow/source-target leg text.
- `S11.160` initial export gate is implemented in Step 5 export selection: current Simulation safety audit is shown in the export window, and `After > 480` blocks export with an explicit warning dialog.
- `S11.158` initial right-panel overview is implemented: the old inline General settings editor was replaced by compact Project/Grid, Notch model, Simulation safety, and Export handoff cards. Editable values now deep-link to `SettingsWindow`, while Simulation safety is projected from the current Simulation workspace instead of being re-derived in the panel.

## 9. UI Implementation Rules

- Use Dev page preview before applying the final layout to the main workspace.
- Do not add inline colors, sizes, or brush literals.
- Add missing spacing/radius/status tokens to `Tokens.axaml`.
- Use two-column form rows and existing form field styles.
- Keep ViewModels thin; safety audit and algorithm summaries should be projected from shared application/UI service models.
- Follow `docs/guides/ui-density-token-rules.md` for normal / compact / narrow panel density and token usage.
