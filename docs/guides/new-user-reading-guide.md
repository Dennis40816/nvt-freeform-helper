# 新手中文閱讀指南

最後更新：2026-05-23

這份文件回答兩個問題：

1. 第一次接手 FreeformHelper 要先看哪些文件。
2. 哪些文件是中文主體或已有中文版本，可以放心列入新手路線。

## 最短路線

如果你只是第一次使用 app 或驗證一個 panel，先看這三份就夠：

| 順序 | 文件 | 適合對象 | 讀完應該知道 |
| --- | --- | --- | --- |
| 1 | `docs/guides/app-user-manual.md` | 第一次操作 app 的使用者 | UI 頁面、Step1~Step5、Simulation / Export 基本流程。 |
| 2 | `docs/guides/settings-parameter-guide.md` | 需要調參數的人 | Settings 每個參數在做什麼、怎麼調、cascade per-IC X/Y 怎麼用。 |
| 3 | `docs/diagrams/notch-simulation/zh-TW/README.md` | 需要看懂 Notch / Simulation 流程的人 | Notch table 與 Simulation 是兩條責任線，並知道要點進哪張圖。 |

這三份都是中文主體；第三份是 diagram library 的 `zh-TW` 版本。

## 演算法 Mermaid 連結

目前演算法 Mermaid 已整理在 repo 內的 Mermaid 文件中；新手優先看中文圖庫，不需要先看英文版。

| 用途 | Mermaid 文件 | 說明 |
| --- | --- | --- |
| Notch / Simulation overview | `docs/diagrams/notch-simulation/zh-TW/README.md` | 目前圖庫入口，區分 Notch table 與 Simulation 兩條責任線。 |
| Notch table 模組地圖 | `docs/diagrams/notch-simulation/zh-TW/notch-table.md` | row identity、candidate、compensation、output projection 的分層圖。 |
| Identity contract | `docs/diagrams/notch-simulation/zh-TW/notch-table-identity.md` | Regular / CAD / SeeRegular 如何收斂成同一份 snapshot。 |
| Candidate assembly | `docs/diagrams/notch-simulation/zh-TW/notch-table-candidate.md` | CadAllocation candidate bucket 的建立流程。 |
| Compensation + target | `docs/diagrams/notch-simulation/zh-TW/notch-table-compensation.md` | ToRegular / ToFull / Stage3 / target allocation 的責任邊界。 |
| Output projection | `docs/diagrams/notch-simulation/zh-TW/notch-table-output.md` | v2.2 canonical、v2.1 compatibility、CAD output grid 與 handoff。 |
| Simulation 模組地圖 | `docs/diagrams/notch-simulation/zh-TW/simulation.md` | Simulation source、apply、audit、UI/export/replay 分層。 |
| Overall flow | `docs/core/notch-overall-flow-mermaid.md` | 維護者用總覽圖；若與 canonical reference 不一致，以 `notch-system-reference.md` 為準。 |

## 依角色閱讀

| 角色 | 建議文件 | 備註 |
| --- | --- | --- |
| 一般使用者 / 驗證者 | `app-user-manual.md` -> `settings-parameter-guide.md` -> `zh-TW` diagram README | 不需要先看 algorithm deep-dive。 |
| 第一次調 Settings | `settings-parameter-guide.md` -> `settings-entry-matrix.md` | 前者給操作與調參建議；後者給設定入口與 single source contract。 |
| 第一次接手維護 workflow | `workflow-pipeline.md` -> `settings-entry-matrix.md` -> `refactor-playbook.md` | 先懂 Step 依賴與 invalidation，再看重構規則。 |
| 第一次 debug Notch / Export | `notch-system-reference.md` -> `freeform-helper-algorithms.md` -> `notch-v21-v22-flow.md` | `notch-system-reference.md` 是現行真值；其他是 deep-dive。 |
| 第一次看 Simulation | `docs/diagrams/notch-simulation/zh-TW/README.md` -> `notch-system-reference.md` 的 Simulation 章節 | 先看圖，再回 canonical reference 對照。 |
| 第一次準備改程式 | `refactor-playbook.md` -> `workflow-pipeline.md` -> `behavior-inventory.md` | 這條路線偏維護者，不是操作手冊。 |

## 中文版本狀態

新手路線只應列入中文主體或已有中文版本的文件。現行可列入新手路線的中文文件如下：

| 文件 | 中文狀態 | 用途 |
| --- | --- | --- |
| `docs/guides/new-user-reading-guide.md` | 中文主體 | 新手入口與閱讀路線。 |
| `docs/guides/app-user-manual.md` | 中文主體 | App 操作手冊。 |
| `docs/guides/settings-parameter-guide.md` | 中文主體 | Settings 參數說明與調適建議。 |
| `docs/diagrams/notch-simulation/zh-TW/README.md` | 中文版本 | Notch / Simulation 圖庫入口。 |
| `docs/core/notch-overall-flow-mermaid.md` | 中文說明 + Mermaid | 維護者用 algorithm overview。 |
| `docs/reference/notch-system-reference.md` | 中文主體 | Notch pipeline / Simulation / Export canonical reference。 |
| `docs/core/freeform-helper-algorithms.md` | 中文主體 | Step1~Step5 演算法 deep-dive。 |
| `docs/core/notch-v21-v22-flow.md` | 中文主體 | Step5 / v2.1 / v2.2 export flow deep-dive。 |
| `docs/core/workflow-pipeline.md` | 中文主體 | Step 依賴與 invalidation 規則。 |
| `docs/guides/settings-entry-matrix.md` | 中文主體 | Settings 入口與 single source matrix。 |
| `docs/guides/refactor-playbook.md` | 中文主體 | 維護與重構流程。 |
| `docs/reference/behavior-inventory.md` | 中文主體 | 現行行為與入口盤點，適合維護者對照。 |

若未來要把某份英文文件加入「新手必讀」，必須先補中文版本或將該文件改為中文主體。

## 先不要看的文件

以下文件不是新手入口，除非你正在追特定歷史或工程問題：

- `docs/archive/`：歷史保留，不代表目前主線；舊 repo scan 報告集中在 `docs/archive/repo-refactor-scans/`。
- `docs/guides/repo-refactor-scan-2026-06-26.md`：最新 repo 掃描紀錄，適合追技術債，不適合入門。
- `docs/generated/`：工具生成，不手動維護。
- `docs/diagrams/notch-simulation/en/`：英文圖庫；新手優先看 `zh-TW`。
- `docs/guides/settings-overview-redesign-plan-*.md`、`post-1.0-*`：設計提案或後續計畫，不是操作流程。

## 維護規則

- `docs/README.md` 只做入口索引，不放長篇內容。
- 新手文件要優先放在 `docs/guides/`，並在本檔與 `docs/README.md` 登記。
- 同一主題若同時有中文與英文，README 的新手入口只連中文。
- 歷史文件要留在 `docs/archive/` 或明確標示非現行主線。
