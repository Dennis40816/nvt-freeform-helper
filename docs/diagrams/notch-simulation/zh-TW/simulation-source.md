# Simulation：Source Frame

> 回到 [Simulation 模組地圖](simulation.md)
> English: [Source frame](../en/simulation-source.md)

這張圖只描述 before frame 的來源。Manual、CSV、Copper 的差異都應在 `BuildSnapshot` 前收斂；進入 apply path 後不再分叉。

```mermaid
%%{init: {"theme": "base", "flowchart": {"curve": "basis", "htmlLabels": true, "nodeSpacing": 44, "rankSpacing": 56}, "themeVariables": {"fontFamily": "Inter, Segoe UI, Noto Sans TC, Arial, sans-serif", "fontSize": "14px", "primaryTextColor": "#172033", "lineColor": "#64748B", "clusterBkg": "#FFFFFF", "clusterBorder": "#CBD5E1"}}}%%
flowchart LR
    SESSION["SimulationWorkspaceSession<br/>NotchTable + RegularGrid"]:::entry

    subgraph SOURCE["Input source"]
        direction TB
        S1{"Source selected?"}:::decision
        S2["Manual baseline<br/>global value + overrides"]:::source
        S3["CSV frame<br/>external diff values"]:::source
        S4["Copper center + radius<br/>CAD overlap projection"]:::source
    end

    subgraph PROJECT["Projection services"]
        direction TB
        P1["BuildManualDataset<br/>regular cell values"]:::compute
        P2["DiffFrameGridProjectionService<br/>CSV -> RegularGrid"]:::compute
        P3["CopperPillarSimulationService<br/>CAD output -> RegularGrid"]:::compute
    end

    FRAME["Imported dataset<br/>one compatible before frame"]:::contract

    SESSION --> S1
    S1 -- "Manual" --> S2 --> P1 --> FRAME
    S1 -- "CSV" --> S3 --> P2 --> FRAME
    S1 -- "Copper" --> S4 --> P3 --> FRAME

    N1["SeeRegular effect is already represented by the session grid and active surface."]:::note
    N2["After this point, source type must not change simulation semantics."]:::note
    SESSION -.-> N1
    FRAME -.-> N2

    classDef entry fill:#F5F3FF,stroke:#7C3AED,color:#1E1B4B,stroke-width:1.5px;
    classDef source fill:#E0F2FE,stroke:#0284C7,color:#0F172A;
    classDef decision fill:#FEF3C7,stroke:#D97706,color:#3A2A0A;
    classDef compute fill:#ECFDF5,stroke:#059669,color:#052E2B;
    classDef contract fill:#EEF2FF,stroke:#4F46E5,color:#1E1B4B,stroke-width:1.5px;
    classDef note fill:#FFFBEB,stroke:#D97706,color:#3A2A0A,stroke-dasharray: 4 3;
```

## Source Boundary

| Source | 收斂入口 |
| --- | --- |
| Manual | `SimulationWorkspaceUseCase.BuildManualDataset(...)` |
| CSV | `DiffFrameGridProjectionService.ProjectFrame(...)` |
| Copper | `CopperPillarSimulationService.ProjectCadOutputToRegularGrid(...)` |
| Shared frame | `SimulationWorkspaceUseCase.BuildSnapshot(...)` 的 input dataset。 |
