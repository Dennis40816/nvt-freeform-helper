# Notch Table：Output Projection

> 回到 [Notch table 模組地圖](notch-table.md)
> English: [Output projection](../en/notch-table-output.md)

這張圖描述 final handoff。v2.2 canonical rows 是主語義；v2.1 payload 和 CAD output grid 都是 projection，不能反向成為 table identity 的來源。

```mermaid
%%{init: {"theme": "base", "flowchart": {"curve": "basis", "htmlLabels": true, "nodeSpacing": 44, "rankSpacing": 56}, "themeVariables": {"fontFamily": "Inter, Segoe UI, Noto Sans TC, Arial, sans-serif", "fontSize": "14px", "primaryTextColor": "#172033", "lineColor": "#64748B", "clusterBkg": "#FFFFFF", "clusterBorder": "#CBD5E1"}}}%%
flowchart LR
    PAYLOAD["Bounded candidate payload<br/>source retain + target legs"]:::contract

    subgraph V22["v2.2 canonical"]
        direction TB
        V1["7-int node rows<br/>semantic source"]:::output
        V2["Continuation rows<br/>large target leg list"]:::output
        V3["Canonical diagnostics<br/>row owner + coverage"]:::artifact
    end

    subgraph PROJ["Compatibility projection"]
        direction TB
        P1["v2.1 projection<br/>9-int legacy payload"]:::compute
        P2["NotchCadOutputFwDiffProjectionService<br/>anchor-only CAD grid"]:::compute
        P3["Display alignment<br/>not target identity"]:::contract
    end

    subgraph HANDOFF["Downstream handoff"]
        direction TB
        H1["NotchTable artifact<br/>single shared result"]:::artifact
        H2["Export C payload<br/>runtime parity tests"]:::output
        H3["Inspector table<br/>display projection"]:::output
        H4["Simulation input<br/>table consumed, not regenerated"]:::output
    end

    PAYLOAD --> V1 --> V2 --> H1
    V1 --> V3 --> H1
    V1 --> P1 --> H1
    V1 --> P2 --> P3 --> H3
    H1 --> H2
    H1 --> H4

    N1["Invariant:<br/>v2.2 canonical row semantics are the source of truth."]:::note
    N2["Projection warning:<br/>CAD output grid is display/export alignment, not target diff identity."]:::note
    V1 -.-> N1
    P3 -.-> N2

    classDef contract fill:#EEF2FF,stroke:#4F46E5,color:#1E1B4B,stroke-width:1.5px;
    classDef compute fill:#ECFDF5,stroke:#059669,color:#052E2B;
    classDef output fill:#F8FAFC,stroke:#475569,color:#111827;
    classDef artifact fill:#F0FDFA,stroke:#0D9488,color:#042F2E,stroke-width:1.5px;
    classDef note fill:#FFFBEB,stroke:#D97706,color:#3A2A0A,stroke-dasharray: 4 3;
```

## Projection Rules

| Projection | 規則 |
| --- | --- |
| v2.2 canonical | 主語義來源；Simulation 和 export 都應消費它。 |
| v2.1 compatibility | 只為 legacy payload 相容，不重新定義 row identity。 |
| CAD output grid | 只對 anchor/display/export 對齊，不覆蓋 target Regular FW Diff。 |
| C export parity | Exported C runtime 必須和 C# simulation path 逐點一致。 |
