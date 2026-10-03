# Simulation: UI / Export / Replay

> Back to [Simulation module map](simulation.md)
> 中文: [UI / export / replay](../zh-TW/simulation-ui-export.md)

This diagram shows how the audit result is shared by UI, export gate, and copper replay. This layer only projects and presents results; it does not re-decide physical plausibility.

```mermaid
%%{init: {"theme": "base", "flowchart": {"curve": "basis", "htmlLabels": true, "nodeSpacing": 44, "rankSpacing": 56}, "themeVariables": {"fontFamily": "Inter, Segoe UI, Noto Sans TC, Arial, sans-serif", "fontSize": "14px", "primaryTextColor": "#172033", "lineColor": "#64748B", "clusterBkg": "#FFFFFF", "clusterBorder": "#CBD5E1"}}}%%
flowchart LR
    AUDIT["SimulationSafetyAuditResult<br/>shared gate model"]:::contract

    subgraph UI["Workspace UI"]
        direction TB
        U1["SimulationSafetyOverviewProjector<br/>summary cards"]:::compute
        U2["SimulationOverlayProjectionBuilder<br/>risk overlays"]:::compute
        U3["Inspector detail<br/>selected diff trace"]:::output
    end

    subgraph EXPORT["Export handoff"]
        direction TB
        E1["NotchExportSelectionViewModel<br/>audit-aware state"]:::compute
        E2["Export gate<br/>risk status visible"]:::gate
        E3["C payload parity<br/>same table semantics"]:::output
    end

    subgraph REPLAY["Copper path replay"]
        direction TB
        R1["Path points<br/>start -> end"]:::source
        R2["BuildCopperDataset<br/>per point"]:::compute
        R3["BuildSnapshot + Analyze<br/>same apply + audit"]:::compute
        R4["Replay summary<br/>max after + violations"]:::output
    end

    AUDIT --> U1 --> U3
    AUDIT --> U2 --> U3
    AUDIT --> E1 --> E2 --> E3
    R1 --> R2 --> R3 --> R4
    R3 -. "produces" .-> AUDIT

    N1["Projection rule:<br/>UI color, sorting, and focus are views over audit data."]:::note
    N2["Replay rule:<br/>each point repeats the same BuildSnapshot -> Simulate -> Analyze path."]:::note
    U3 -.-> N1
    R3 -.-> N2

    classDef source fill:#E0F2FE,stroke:#0284C7,color:#0F172A;
    classDef compute fill:#ECFDF5,stroke:#059669,color:#052E2B;
    classDef gate fill:#FFF7ED,stroke:#EA580C,color:#431407,stroke-width:1.5px;
    classDef output fill:#F8FAFC,stroke:#475569,color:#111827;
    classDef contract fill:#EEF2FF,stroke:#4F46E5,color:#1E1B4B,stroke-width:1.5px;
    classDef note fill:#FFFBEB,stroke:#D97706,color:#3A2A0A,stroke-dasharray: 4 3;
```

## Projection Rules

| Area | Rule |
| --- | --- |
| Overlay | Projects `SimulationSafetyAuditResult` and simulation cells only. |
| Inspector | Shows the selected diff action trace without recalculating flow. |
| Export gate | Presents risk status from the same audit snapshot. |
| Copper replay | Runs the same apply + audit pipeline for every path point. |
