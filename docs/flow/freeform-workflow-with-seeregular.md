# Freeform Workflow (with SeeRegular)

This diagram reflects the current runtime path in `beta0.5` and includes the SeeRegular (`Regular visibility mask`) branch.

```mermaid
flowchart TD
    A["Open DXF / Load Project"] --> B["TriggerGridRebuildAsync()"]
    B --> C["RebuildGridAsync()"]
    C --> C1["RefreshRegularVisibilityMask(grid)"]
    C --> C2["NotifySimulationWorkspaceSourceChanged()"]

    M1["Import Regular Visibility Mask (SeeRegular.csv)"] --> M2["RegularVisibilityMaskService.Load(...)"]
    M2 --> M3{"Mask loaded?"}
    M3 -- "Yes" --> M4["SetRegularVisibilityMaskEnabled(true)"]
    M3 -- "No / invalid" --> M5["SetRegularVisibilityMaskEnabled(false)"]
    M4 --> M6["RefreshCadOutputFwDiffIndexingForRegularVisibilityMaskChange()"]
    M5 --> M6

    S1["Step1 MatchAsync()"] --> S2["_padMatchService.Match(...)"]
    S2 --> D["UpdateCadOutputFwDiffIndexing(...)"]
    M6 --> D

    D --> D1["BuildCadStrictMatchDiffById(...)"]
    D1 --> D2["DxfRegularMaskAuditService.BuildAssignmentDecisions(...)"]
    M4 -. "active mask pad IDs" .-> D2

    D2 --> W["BuildWorkflowDataSnapshot()"]
    W --> W1["CadOutputFwDiffIndexByCadId + AssignmentDecisions + ActiveMask"]

    S3["Step2 AutoDetect / Project-load replay"] --> W

    W1 --> N1["GenerateCurrentNotchTableAsync(...)"]
    N1 --> N2["_notchExportService.Generate(...)"]
    N2 --> N3["NotchCadOutputFwDiffProjectionService.Project(...)<br/>(anchor-only + CAD output grid)"]

    N3 --> E1["Step5 Export flow"]
    E1 --> E2["OpenNotchExportSelectionAsync (Stage 3)"]
    E2 --> E3["PickSaveNotchPathAsync + write file"]

    N3 --> SIM1["Simulation workspace flow"]
    SIM1 --> SIM2["CreateSimulationWorkspaceSessionAsync()"]
    SIM2 --> SIM3["Before/After / Delta review"]

    C2 --> SC0["WorkspaceDerivedSourceChanged event"]
    SC0 --> SC1["Shell source-change drain/coalescing"]
    SC1 --> SC2["Simulation.PrewarmWorkspaceAsync()"]
    SC1 --> SC3["CoordinatePlanner.PrewarmWorkspaceAsync()"]

    LP1["ApplyLoadedProjectStateAsync()"] --> LP2["BeginSimulationWorkspaceSourceChangeBatch()"]
    LP2 --> LP3["Load settings + DXF apply + rebuild + Step1/Step2 replay"]
    LP3 --> LP4["End batch -> emit one source change"]
    LP4 --> SC0
```

## Verification Notes
- SeeRegular is included in Step4 assignment through `GetActiveRegularVisibilityMaskPadIds()` and `DxfRegularMaskAuditService.BuildAssignmentDecisions(...)`.
- Step5 generator now consumes `CadOutputFwDiffIndexByCadId` as row source diff. In `CadAllocation` mode, source diff is already final when rows are built.
- `NotchCadOutputFwDiffProjectionService.Project(...)` only aligns row anchor diff to CAD output FW diff and builds the display-only CAD output grid. It no longer rewrites target diff.
- Simulation applies `source = CAD Output FW Diff` and `target = Regular FW Diff`, so SeeRegular only changes the source memory/output side, not the geometry target side.
- Project-load source changes are batched (`BeginSimulationWorkspaceSourceChangeBatch` / `End...`) and Shell prewarm is coalesced to avoid burst rebuilds.
