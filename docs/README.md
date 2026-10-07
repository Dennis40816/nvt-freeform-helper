# Docs 導覽
最後更新：2026-07-20

## 新手中文入口

第一次接手或第一次操作 app，先看這份：

1. `docs/guides/new-user-reading-guide.md`
   - 新手中文閱讀路線、角色分流、哪些文件先不要看。

最短操作路線：

1. `docs/guides/app-user-manual.md`
   - UI 頁面、Step1~Step5、Simulation / Export 基本流程。
2. `docs/guides/settings-parameter-guide.md`
   - Settings 全參數功能、調適建議與 cascade per-IC X/Y 操作。
3. `docs/diagrams/notch-simulation/zh-TW/README.md`
   - Notch table 與 Simulation 的中文流程圖入口。

這個區塊只列中文主體或已有中文版本的文件；若新手必讀文件只有英文，先補中文版本再加入。

## 文件結構

- `docs/core/`
  - 核心規格、演算法 deep-dive、workflow pipeline。
- `docs/diagrams/`
  - Mermaid 流程圖；新手優先看 `zh-TW`。
- `docs/guides/`
  - 使用手冊、設定指南、重構手冊、測試分類與閱讀指南。
- `docs/performance/`
  - 效能基線、量測與優化筆記。
- `docs/reference/`
  - canonical reference、外部契約、CLI 設計與行為盤點。
- `docs/generated/`
  - 工具生成文件，不手動維護。
- `docs/archive/`
  - 已退出主線但保留追溯價值的歷史文件。

## 維護者深入路線

這組是接手維護或 debug 時的閱讀順序，不是第一次使用 app 的最短路線。

1. `docs/reference/notch-system-reference.md`
   - Notch pipeline / diff identity / simulation / export contract 的唯一 canonical reference。
2. `docs/core/freeform-helper-algorithms.md`
   - Freeform Helper 主流程與 Step1~Step5 演算法全解，含對應程式入口。
3. `docs/guides/app-user-manual.md`
   - 給一般使用者/驗證者的完整操作手冊，從載入專案到 Simulation / Export / Diagnostics。
4. `docs/core/notch-v21-v22-flow.md`
   - Step5 flow、bucket、continuation 與 export path 的唯一 deep-dive。
5. `docs/diagrams/notch-simulation/zh-TW/README.md`
   - Notch table 計算與 Simulation 驗證的中文 Mermaid 圖庫入口。
6. `ROADMAP.md`
   - 唯一的進度表：修正線、產品功能、整合 Core 三條線，每項附目標版本、狀態與 PR 連結。
7. `docs/reference/refactor-contract.md`
   - 1.0.x 重構契約：zero-diff 政策、Firmware Q7 契約、gate G0～G6、golden 更新規則與未完成 slice 的規格。
8. `docs/guides/post-1.0-tool-workbench-redesign-plan-2026-04-30.md`
   - 1.0 後 Tool Workbench 重做規格：Simulation 完整性與 Coordinate artifact 產生工具。
9. `docs/guides/refactor-playbook.md`
   - 重構與交接標準流程（分支、驗證、提交）。
10. `docs/guides/settings-parameter-guide.md`
   - SettingsWindow 全參數功能、調適建議與 cascade per-IC X/Y 操作說明。
11. `docs/core/workflow-pipeline.md`
   - Step 依賴關係與 invalidation 規則。

## 常用補充文件

- `docs/core/notch-v21-algorithm.md`
- `docs/core/notch-v22-algorithm.md`
- `docs/core/notch-validation-flow.md`
- `docs/core/notch-overall-flow-mermaid.md`
- `docs/guides/settings-entry-matrix.md`
- `docs/guides/test-categories.md`
- `docs/performance/perf-baseline-howto.md`
- `docs/performance/code-size-baseline-1.3.0.md`
- `docs/performance/regular-spatial-index-notes.md`
- `docs/reference/behavior-inventory.md`
- `docs/reference/cad-reg-numbering-reference.md`
- `docs/reference/notch-overlay-h5-design.md`
- `docs/reference/runtime-cli-plan.md`
- `example/BOE36.35/notch_export_v21_current.c`
- `example/BOE36.35/notch_export_v22_current.c`

## 先不要從這裡開始

- `docs/archive/`：歷史保留，不代表目前主線；舊 repo scan 報告集中在 `docs/archive/repo-refactor-scans/`。
- `docs/guides/repo-refactor-scan-2026-06-26.md`：最新掃描紀錄，適合追技術債，不適合入門。
- `docs/generated/`：工具生成，不手動維護。
- `docs/diagrams/notch-simulation/en/`：英文圖庫；新手優先看 `zh-TW`。
- `docs/guides/*redesign-plan*.md`、`docs/guides/post-1.0-*`：設計提案或後續計畫，不是操作手冊。

## 已清理與收斂
- 已刪除不再適用文件：`docs/non-notch-refactor-batches.md`
- 已刪除已被 canonical/deep-dive 完整覆蓋的文件：
  - `docs/core/notch-algorithm-overview.md`
  - `docs/core/freeform-notch-spec.md`
  - `docs/code/notch_v21.md`
- 原本平鋪在 `docs/` 根目錄的規格文件，已依用途分流到 `core/guides/performance/reference/archive`

## 維護原則
- 只保留「仍在現行流程中會被直接引用」的文件在 `core/` 或 `guides/`。
- 可由工具或程式重新產生的內容，不長期存放於 `docs/`。
- 舊版提案若仍需追溯，一律移入 `docs/archive/`，避免主線閱讀干擾。

