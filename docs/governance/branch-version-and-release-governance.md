# 分支、版本與發佈治理

狀態：1.3.x 分支與現有 gate 的文件；發佈自動化仍屬 `TODO.md S15.005e`。

## 由產品影響選擇版本

1.3.0～1.3.5 的成果、順序及退出條件由 `docs/guides/refactor-roadmap-1.3.x.md` 定義，進度由 `TODO.md` 維護。不得跳過前版退出條件；需要更動輸出的 correctness 工作不得混入零差異的 refactor／UI commit。`2.0.0 = 開始共用核心架構` 是 owner 於 2026-10-02 的暫定目標，尚非完成的版本契約。

## 維持單一版本身分

`TODO.md S15.005a` 的 `VERSION` 初始值及 repo 外固定 test area 仍待處理。本庫目前不能宣稱範本的 `VERSION`、tag、package、manifest 與 Catalog 映射已生效；版本身分與發佈產物的正式政策須隨 `S15.005e` 決定。

## 分支權限與工作方向

- `main` 是預設分支，只保存已發佈版本；目前的 minor-line trunk 是 `1.3.x`。獨立工作使用 `feature/<version>/<topic>`，以對應 trunk 為 PR 目標，不直接把 feature 合入 `main`。
- 1.3.x 的工作順序先處理 `S15.*` 基準修復與公版對齊，再進入未完成的 `R13.*`；owner 已決定，只剩 owner-only `S15` 項目時可以開始 `R13`。
- 本庫尚未完成 release branch、tag 與 release workflow 的執行契約；`S15.005e` 完成前，以現行 CI 與 roadmap gate 判斷工作完成，不引用範本的發佈命令。

## 將工作納入版本與 PR

每個 PR 說明成果、關聯 issue／TODO、影響的 workflow／contract 與驗證。審查與合併條件以 [貢獻指南的「合併邊界」](../../CONTRIBUTING.md#合併邊界owner-決定2026-10-02) 為準。

## 發佈復原與 release notes

本庫的 release workflow、rehearsal、復原步驟與 release notes 格式尚未在 `S15.005e` 落地。當前工作只可報告已完成的本地驗證與 CI 證據；不得把範本的 tag 復原、簽章、package 或 release closure 流程當作本庫已核准政策。

## 發佈後的 PR 與分支清理

大批量關閉 PR、刪除遠端分支及 release merge-back 的授權與步驟尚未由本庫決定；另見 `TODO.md S15.005d`、`S15.005e`。
