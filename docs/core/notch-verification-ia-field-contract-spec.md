# Notch Verification Information Architecture and Field Contract Spec (Step 3/4/5/6 + Simulation + Export)
Last updated: 2026-04-06
Status: Draft (ready for UI implementation)

## 1. Background and Problems
Verification information is currently scattered across multiple surfaces:
- Right panel Step 3 / Step 4 / Step 5 / Step 6
- Standalone `Analyze mapping` window
- Standalone `Export notch rows` window
- `Simulation` workspace

The core problem is not "insufficient data", but "different projection structures for the same data across surfaces":
- The decision, repair, and notch impact for the same CAD/row are split across different views; users need to switch screens to piece together the full context.
- Infrequently used text descriptions appear directly in the main workflow, making information density too high.
- Editable and read-only fields are mixed together, making it difficult for users to tell "whether this field can be changed" and "what a change will affect".

## 2. Goals and Non-goals
Goals:
- Establish a single "field contract" that clearly defines each field's source, editability, change action, and downstream impact.
- Use "table + right review panel" as the main verification UI pattern; keep only frequently used information in the main view and move long descriptions to info icons/tooltips.
- Preserve the existing single-path computation contract without adding parallel recomputation paths.

Non-goals:
- This spec does not change the notch core algorithms (v2.1/v2.2 math).
- This spec does not rewrite the Step 3 geometry preview rendering engine; it only defines how verification information is projected.

## 3. Single Source of Truth Layers
- L0 geometry and mask truth
  - `Step1` match candidates + `RegularVisibilityMaskService`。
- L1 diff assignment / decision truth
  - `DxfRegularMappingUseCase.Analyze(...)` + `DxfRegularMaskAuditService`。
- L2 notch table truth
  - The `GenerateCurrentNotchTableAsync(...)` output `NotchTable` (`_lastGeneratedNotchTable` is the latest snapshot).
- L3 validation trace truth
  - `NotchValidationUseCase.BuildBucket(...)` + `NotchValidationTraceService.BuildTrace(...)` (reads only L2 without recomputing notch).
- L4 simulation projection truth
  - `SimulationWorkspaceSession(Grid, Table, NullDiff, ActiveRegularPadIds, CadOutputFwDiffAssignmentDecisions)`。

## 4. Proposed Information Architecture (IA)
## 4.1 Main Structure
- Left: `Verification table` (sortable/filterable/searchable)
- Right: `Review panel` (single-record details + actions + before/after)
- Top: `Summary chips` (statistics + status)
- Long explanations: only in an `info icon` (shown on hover)

## 4.2 View Consolidation Principles
- Step 4 (decision/repair) and Step 5/6 (export/validation trace) use the same row identity: `IC + Diff + Row + CAD/REG`.
- Simulation keeps a separate workspace, but its inspector field names match the Verification table.
- Do not insert algorithm state directly as long strings; use structured fields + reason badges instead.

## 4.3 Right Step Rail (Nodes + Connections) Contract
- The right panel has two fixed layers:
  - Upper: `Verification summary` (fixed at the top, sticky)
  - Lower: `Step rail` (vertical nodes + connections, with each node representing a verification stage)
- Step rail node order (display must not skip numbers):
  - `Step 3`：Notch input / stage readiness
  - `Step 4`：Diff assignment / mapping decision
  - `Step 5`：Notch table row integrity
  - `Step 6`：Validation trace consistency
  - `Simulation`：Before/After/Delta sanity check
- Each node must have at least four fields:
  - `status`：`pass / warning / blocked / pending`
  - `what to verify`: a one-sentence verification goal (must not exceed two lines)
  - `key checks`: 2~4 checkable verification items
  - `open action`: open the corresponding detail view (table filter / report / trace / simulation focus)
- Node click behavior:
  - Automatically apply the corresponding filter to the left table (same row identity)
  - Switch the right detail area to that step's view card
  - Must not trigger parallel recomputation; read only existing projectors/snapshots

## 4.4 Summary Area (Pinned to Top) Contract
- Summary stays at the top right and contains only frequently used summaries, without long paragraphs:
  - `current mode`（geometry-only / csv-constrained）
  - `overall status`（pass / warning / blocked）
  - `open issues count` (aggregated by reasonCode)
  - `last table revision` (corresponding `_lastGeneratedNotchTable` version/timestamp)
  - `active step` (the node the user is currently reviewing)
- Summary is read-only and cannot be edited; all changes must be triggered through step actions below it.

## 5. Field Contract
## 5.1 Assignment / Decision Fields (Shared by Step4, Simulation, and Runtime)
| Field | Source (single source) | Editable | Change action | Downstream impact |
| --- | --- | --- | --- | --- |
| `mode` (`geometry-only`/`csv-constrained`) | `DxfRegularMaskAuditService` decision | No | None | Affects reason interpretation and repair rules |
| `reasonCode` | `DxfRegularMaskAuditService` | No | None | Determines UI badge and sort priority |
| `decisionSource` (`seed/auto-repaired/manual-override/propagated-override`) | `DxfRegularMaskAuditService` + overrides state | No (derived) | `SetCadOutputFwDiffIndexOverride(...)`, clear override | Affects whether auto-repair can overwrite it |
| `rawBestDiff` | mapping analyze result | No | None | Compares differences between seed and repair |
| `maskedBestDiff` | mapping + active mask | No | None | CSV constrained visible candidates |
| `primaryAssignedDiff` | assignment decision | Indirectly editable | `OffsetSelectedCadOutputFwDiffIndicesCommand`, `Apply*DiffOverrides*`, single-record apply | Directly affects notch table generation |
| `passiveCompensationDiff` | local repair/passive compensation signal | No (currently) | None | For review, with manual apply when necessary |
| `repairSuggestion` | local repair score output | No (suggested value) | `ApplyCadOutputFwDiffOverridesCommand`, `ApplySegmentDiffOverridesCommand` | Becomes a manual override after application |
| `confidence` | repair/decision score | No | None | Determines auto-apply/needs-review |
| `detectedOffset` / `offsetSupportRatio` | segment offset detector | No | None | Entry point and explainability for segment repair |

## 5.2 Notch Export Row Fields (Step5 + Export Window)
| Field | Source (single source) | Editable | Change action | Downstream impact |
| --- | --- | --- | --- | --- |
| `rowNumber` | `NotchTable.Rows` index | No | None | Cross-reference key for trace and firmware |
| `version` (`V21`/`V22`) | `NotchTableRow.Version` | No | None | Export payload format |
| `icIndex` / `diffIndex` | `NotchTableRow` | No | None | row grouping and lookup |
| `regularPadIndex` / `cadPadId` | `NotchTableRow` | No | None | mapping provenance |
| `payload(values/v22 node)` | `NotchTableRow.Values` + `V22Node` | No | None | Actual firmware computation input |
| `status` (`linked`/`warning`/`noCad`/`legacy`) | row projector | No | None | UI filtering and acceptance focus |
| `isSelected` (export selection) | `NotchExportSelectionViewModel` UI state | Yes (UI only) | row/group select/unselect | Only affects the current export scope, without changing the algorithm |

## 5.3 Validation Trace Fields (Step6)
| Field | Source (single source) | Editable | Change action | Downstream impact |
| --- | --- | --- | --- | --- |
| `validationRegularPadId` | Step6 input state | Yes | `UseSelectedRegularForNotchValidationCommand`, manual input | Determines the trace query target |
| `direct/incoming/outgoing rows` | `NotchValidationTraceService.BuildTrace(...)` | No | `AnalyzeNotchValidationCommand` (rebuild) | Display only, without writing back to notch |
| `trace counts` | trace result aggregation | No | Analyze/clear | Verification summary and sorting |

## 5.4 Simulation Verification Fields
| Field | Source (single source) | Editable | Change action | Downstream impact |
| --- | --- | --- | --- | --- |
| `Before/After/Delta view` | `SimulationWorkspaceUseCase` snapshot + `SimulationWorkspaceSession.Table` | Yes (view mode) | canvas view switch | Only changes the display, without changing the table |
| `color scale min/max` | Currently visible cell distribution (same projection) | No (automatic) | View/filter changes trigger reprojection | Only affects colors, without changing values |
| `selected regular impact list` | snapshot impact projector | No | row/cell selection | Helps trace notch row provenance |

## 6. Action -> Field Impact Matrix
| Action | Fields written | Recomputation layer | Impact scope |
| --- | --- | --- | --- |
| `ImportRegularVisibilityMaskCommand` | mask state（path/content/enabled candidate set） | L0 -> L1 | Step4/Simulation decision surface |
| `ClearRegularVisibilityMaskCommand` | Clears mask state | L0 -> L1 | Step4/Simulation returns to geometry-only |
| `OffsetSelectedCadOutputFwDiffIndicesCommand` | manual override diff | L1 | Affects L2 notch table |
| `ApplyCadOutputFwDiffOverridesCommand` | manual override diff (batch) | L1 | Affects L2/L3/L4 |
| `ApplySegmentDiffOverridesCommand` | manual override diff (segment) | L1 | Affects L2/L3/L4 |
| `AnalyzeIndexMappingCommand` | No persistent write (produces a report snapshot) | L1 read | Only updates the Step4 report display |
| `ExportNotchCommand` | `_lastGeneratedNotchTable` | L2 | Affects Step5 summary, Step6 source, and Simulation session source |
| `AnalyzeNotchValidationCommand` | validation trace UI state | L3 read | Updates the Step6 display |
| `RefreshNotchCanvasPreviewCommand` | Step3 preview cache | Step3 display path | Should not write back to L1/L2 |

## 6.1 Existing Features to Preserve (No Regressions)
All of the following behaviors must remain after the right-side IA redesign; the UI redesign must not remove them:

- Step 3（Notch compensation）
  - `Recompute focused CAD (Step 3)`（`RefreshNotchCanvasPreviewCommand`）。
  - stage switching (previous/next layer) and auto-play (including interval).
  - `Clear Step 3` clears Step 3 and downstream state.
  - The Step 3 entry point can still navigate to the corresponding Settings tab.

- Step 4（Index diagnostics）
  - Selecting `Select` for a duplicate group must focus the corresponding CAD (`SelectStep4DuplicateDiffGroupCommand`).
  - Preserve batch diff shift (`OffsetSelectedCadOutputFwDiffIndicesCommand`).
  - `Analyze mapping` still opens the mapping report (must not disappear).
  - Preserve all actions in the report:
    - locate row target
    - apply override / clear override
    - apply diff repair (single record)
    - apply segment repairs (segment)
    - apply visible repairs (currently visible)

- Step 5（Export）
  - Preserve the main `Export` command (`ExportNotchCommand`).
  - Preserve the row filter / search / sort / select / use shown only contract.
  - Preserve the workspace preview/locate callback after row selection (`SelectPreviewRowCommand`).
  - Preserve the `Hide panel (inspect AA)` + `Ctrl+Shift+E` restore workflow.

- Step 6（Validation quick trace）
  - Preserve the three main commands: `Use Selected REG`, `Analyze`, and `Clear`.
  - Preserve `DIRECT / IN / OUT` grouping.
  - Clicking a validation row must still focus source/target pads (`FocusNotchValidationItemCommand`).

- Simulation
  - Clicking a regular on the canvas must update the right inspector and notch impact together.
  - Preserve `Fit canvas` and playback (prev/play/next/slider/fps).
  - Preserve manual override (apply selected / clear selected / clear all).
  - Preserve `Before / After / Delta / Changed only` and color mode switching.

## 6.2 Interaction Event Contract (Click/Focus)
- Any `Open action` or row click that triggers focus must use the existing locate/focus path, without introducing a second focus algorithm.
- A focus failure must retain explicit status text (for example, target not visible); silent failure is not allowed.
- Clicking a Step rail node may only "switch projection/filter/detail" and must not directly change algorithm data.

## 7. Display Rules (Avoid Information Overload)
- Keep only 3 types of elements in the main view:
  - `Summary chips` (counts and status)
  - `Table` (sortable/filterable)
  - `Review actions` (single-record/batch)
- Free-text explanations longer than two lines are prohibited in the main workflow; long explanations must go in an info icon.
- All reasons/statuses use badges: `reasonCode`, `decisionSource`, `status`.
- The default table has no more than 8 columns; advanced fields go in the right panel.

## 8. Current Gaps (To Address in UI Implementation)
- Step4, Step5, and Step6 have not yet been unified into a verification workspace with the same row identity.
- Cross-links between Export and Validation trace are not intuitive enough (currently, users must switch between multiple sections to see the whole picture).
- The `before/after` apply preview does not yet use the same presentation syntax in the Step4 report and Export window.
- Simulation inspector and Step4 report field names are not yet fully consistent (contract alignment is needed).

## 9. Suggested Implementation Slices
- Phase A (Implement the Data Contract)
  - Build a `VerificationRow` projector (read-only integration of L1+L2+L3 key fields, without recomputation).
  - Unify field names and badge tokens.
- Phase B (Step4 Report Structure Redesign)
  - Change to table + right review panel.
  - Move existing long descriptions to info icons.
- Phase C (Connect Step5/Step6)
  - Connect Export rows and Validation trace through the same row key.
  - Support locating the corresponding export row directly from the trace.
- Phase D (Align Simulation)
  - Switch Simulation inspector to read the field contract with matching names.
  - Align color/impact text with the verification table's key identifiers.

## 10. Verification Criteria
- Each field has only one source projector across Step4/Step5/Step6/Simulation.
- The impact scope of any action can be fully traced through the "Action -> Field Impact Matrix".
- Long descriptive text no longer appears in main workflow screens; explanations use info icon + tooltip.
- Users can answer three questions in a single verification workflow:
  - What is the current value (current)
  - Why does it have this value (reason/source)
  - What will a change affect (impact)
