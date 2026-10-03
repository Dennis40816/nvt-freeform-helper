# Notch Table: Identity Contract

> Back to [Notch table module map](notch-table.md)
> 中文: [Identity contract](../zh-TW/notch-table-identity.md)

This diagram verifies that every identity source converges into `WorkflowDataSnapshot` before Step5 rows are generated. Table generation, inspector, and export should not re-derive the same result from partial UI state.

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

| Item | Responsibility |
| --- | --- |
| `RegularGrid` | Provides stable IC / Regular FW Diff identity. |
| Step1 mapping | Defines the computable CAD-to-Regular scope. |
| SeeRegular mask | Restricts the active surface without changing source diff semantics. |
| `WorkflowDataSnapshot` | The only frozen input contract for Step5 table calculation. |
