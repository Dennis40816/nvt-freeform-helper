# Simulation：Physical Audit

> 回到 [Simulation 模組地圖](simulation.md)
> English: [Physical audit](../en/simulation-audit.md)

這張圖是 release gate 的核心。`SimulationSafetyAuditService.Analyze(NotchApplySimulationResult)` 同時讀 Cells 與 Actions，正式檢查 EMS cap、net-flow residual、geometry coverage。

```mermaid
%%{init: {"theme": "base", "flowchart": {"curve": "basis", "htmlLabels": true, "nodeSpacing": 44, "rankSpacing": 56}, "themeVariables": {"fontFamily": "Inter, Segoe UI, Noto Sans TC, Arial, sans-serif", "fontSize": "14px", "primaryTextColor": "#172033", "lineColor": "#64748B", "clusterBkg": "#FFFFFF", "clusterBorder": "#CBD5E1"}}}%%
flowchart LR
    RESULT["NotchApplySimulationResult<br/>Cells + Actions"]:::artifact

    subgraph AUDIT["SimulationSafetyAuditService.Analyze"]
        direction TB
        A1["Read cell totals<br/>Before / After / Delta"]:::compute
        A2["Read action flow<br/>source retain + target legs"]:::compute
        A3["Build diff audit rows<br/>one row per risk diff"]:::compute
    end

    subgraph GATES["Formal gates"]
        direction TB
        G1["EMS cap<br/>After <= 480"]:::risk
        G2["Global flow diagnostic<br/>total before / after"]:::risk
        G3["Net-flow residual<br/>cell delta vs actions"]:::risk
        G4["Geometry coverage cap<br/>retained + incoming <= 120%"]:::risk
    end

    OUT["SimulationSafetyAuditResult<br/>severity + diff details"]:::contract

    RESULT --> A1 --> A3
    RESULT --> A2 --> A3
    A3 --> G1 --> OUT
    A3 --> G2 --> OUT
    A3 --> G3 --> OUT
    A3 --> G4 --> OUT

    N1["Global 400 is diagnostic context, not a correctness target."]:::note
    N2["High-risk diff must be explainable by EMS, net-flow, or geometry coverage evidence."]:::note
    G2 -.-> N1
    OUT -.-> N2

    classDef compute fill:#ECFDF5,stroke:#059669,color:#052E2B;
    classDef risk fill:#FEE2E2,stroke:#DC2626,color:#450A0A,stroke-width:1.3px;
    classDef contract fill:#EEF2FF,stroke:#4F46E5,color:#1E1B4B,stroke-width:1.5px;
    classDef artifact fill:#F0FDFA,stroke:#0D9488,color:#042F2E,stroke-width:1.5px;
    classDef note fill:#FFFBEB,stroke:#D97706,color:#3A2A0A,stroke-dasharray: 4 3;
```

## Audit Gates

| Gate | 目的 |
| --- | --- |
| EMS cap | 防止單一 diff after value 超過安全上限。 |
| Net-flow residual | 確認 cell delta 能由 source retain / target action 解釋。 |
| Geometry coverage | 防止 retained + incoming coverage 過度膨脹。 |
| Global flow | 保留全域診斷訊號，但不把 400 當正確性目標。 |
