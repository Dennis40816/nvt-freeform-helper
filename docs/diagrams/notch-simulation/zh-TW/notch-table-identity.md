# Notch Table：Identity Contract

> 回到 [Notch table 模組地圖](notch-table.md)
> English: [Identity contract](../en/notch-table-identity.md)

這張圖確認 Step5 在開始產生 rows 前，所有身分來源都先收斂成 `WorkflowDataSnapshot`。後續 table、inspector、export 不應從局部 UI 狀態重新推導同一個結果。

```mermaid
%%{init: {"theme": "base", "flowchart": {"curve": "basis", "htmlLabels": true, "nodeSpacing": 46, "rankSpacing": 58}, "themeVariables": {"fontFamily": "Inter, Segoe UI, Noto Sans TC, Arial, sans-serif", "fontSize": "14px", "primaryTextColor": "#172033", "lineColor": "#64748B", "clusterBkg": "#FFFFFF", "clusterBorder": "#CBD5E1"}}}%%
flowchart LR
    subgraph RAW["Raw geometry inputs"]
        direction TB
        CAD["CAD pads<br/>CadPadId + geometry"]:::source
        REG["Regular pads<br/>IC + Regular FW Diff"]:::source
        STEP1["Step1 mapping<br/>RegularToCad / CadToRegular"]:::source
    end

    subgraph SURFACE["Active surface policy"]
        direction TB
        MATCH["Match-scope active set<br/>RegularToCad.Keys"]:::compute
        SEEREG{"SeeRegular enabled?"}:::decision
        MASK["Visibility mask<br/>active regular ids"]:::compute
        ACTIVE["Final active regular surface<br/>bounded input domain"]:::contract
    end

    subgraph DIFF["Source diff identity"]
        direction TB
        RAWDIFF["Raw anchor FW Diff<br/>fallback only"]:::source
        CADOUT["CAD Output FW Diff<br/>override + strict seed"]:::compute
        STRICT["Strict source diff map<br/>CadPadId -> SourceDiff"]:::contract
    end

    SNAP["WorkflowDataSnapshot<br/>single frozen identity model"]:::artifact

    CAD --> STEP1 --> MATCH --> SEEREG
    REG --> MATCH
    SEEREG -- "Yes" --> MASK --> ACTIVE
    SEEREG -- "No" --> ACTIVE
    CAD --> RAWDIFF --> STRICT
    CAD --> CADOUT --> STRICT
    ACTIVE --> SNAP
    STRICT --> SNAP
    STEP1 --> SNAP

    G1["Guarantee:<br/>CadAllocation source diff uses CAD Output FW Diff first."]:::note
    G2["Guarantee:<br/>Snapshot cache invalidates when mapping, layer, visibility, or project-load identity changes."]:::note
    STRICT -.-> G1
    SNAP -.-> G2

    classDef source fill:#E0F2FE,stroke:#0284C7,color:#0F172A;
    classDef compute fill:#ECFDF5,stroke:#059669,color:#052E2B;
    classDef decision fill:#FEF3C7,stroke:#D97706,color:#3A2A0A;
    classDef contract fill:#EEF2FF,stroke:#4F46E5,color:#1E1B4B,stroke-width:1.5px;
    classDef artifact fill:#F5F3FF,stroke:#7C3AED,color:#1E1B4B,stroke-width:1.5px;
    classDef note fill:#FFFBEB,stroke:#D97706,color:#3A2A0A,stroke-dasharray: 4 3;
```

## Contract

| 項目 | 責任 |
| --- | --- |
| `RegularGrid` | 提供穩定 IC / Regular FW Diff 身分。 |
| Step1 mapping | 決定 CAD 與 Regular 的可計算對應範圍。 |
| SeeRegular mask | 只限制 active surface，不改變 source diff 語意。 |
| `WorkflowDataSnapshot` | Step5 table 計算的唯一 frozen input contract。 |
