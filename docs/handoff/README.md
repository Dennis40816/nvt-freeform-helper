# Agent 交接協定

本目錄保存跨 session 或多人工作的即時交接狀態：誰負責什麼、在哪個 branch／head、已完成哪些驗證與還有什麼 gate。產品行為以 `docs/reference/` 契約及現行 roadmap 為準；可執行待辦與狀態由 `ROADMAP.md` 維護，不在交接紀錄複製第二份 backlog。

## 檔案與責任

- `<version>.md`：版本協調板，記錄 base、工作流、負責者、待決事項與跨分支阻塞。
- `<version>/<workstream-id>.md`：單一工作流的交接紀錄，由該工作流負責者維護。
- `bugs/ledger.md`：目前為空的 bug ledger 索引。發現 bug 時新增 `bugs/BUG-<yyyymmdd>-<slug>.md`，再在 ledger 加入該 ID 與連結；每個 bug 一檔，避免並行寫入衝突。

## 交接紀錄必備內容

每份工作流紀錄先寫明以下資訊，讓接手者不需依賴聊天紀錄猜測狀態：

1. **成果與非目標**：可觀察的完成條件、明確不處理的範圍。
2. **Branch、worktree 與基準**：base commit、目前 head、目標分支；使用相對工作區標識，不記錄私人機器絕對路徑。
3. **先讀材料**：適用的 `AGENTS.md`、reference contract、roadmap、TODO／issue，以及先前驗證證據。
4. **執行資訊**：已知的 runtime／model 設定、工作分配原因；未知時如實標示未知。
5. **驗收與待決事項**：預期檢查、證據位置與需要 owner 決定的事項。

## Checkpoint 格式

每次交接在紀錄末尾追加 checkpoint，至少列出：日期／摘要；狀態（planned、local、verified、integrated、published）；commit 與 head；實際命令、結果及其對應 SHA；已修改與未追蹤檔案；審查發現；尚未完成的 gate、阻塞及答覆者；下一個具體動作。未 commit 的修改不能僅靠其他 branch 的 Git 歷史讀到，應在紀錄中明說。

檢視其他 branch 已提交的交接紀錄可使用 `git show <branch>:docs/handoff/<version>/<workstream-id>.md`。交接紀錄僅報告實際驗證的狀態；`verified` 不等於已整合或已發佈。

## Bug ledger

發現與既有契約、測試或文件相矛盾的行為，或 gate 因錯誤原因失敗時，以一個 bug ID 建立一份檔案；未確認的線索標為 `suspected`。

公開交接與 bug 紀錄僅保留可分享的 ID、hash 和路徑參照，不寫入私有 `example/` 內容、憑證或個人工作站路徑。

## 範本導入的建議流程（尚未經 owner 確認）

- 交接時可記錄可執行的 edit、local commit、push、PR、GitHub write 等動作、可寫範圍與需要的人員／golden gate；交接紀錄本身不擴大授權，範圍外修改的處理方式仍待確認。
- Bug 檔案可採以下欄位格式：

```text
# BUG-<yyyymmdd>-<slug>: <標題>
Status: suspected | open | fixing | fixed | wontfix | duplicate
Severity: P0 | P1 | P2 | P3
Found: <日期、工作、branch@sha>
Where: <路徑與行號或命令>
Observed: <實際行為>
Expected: <依據契約及預期行為>
Evidence: <可重現命令與結果或程式碼位置>
Owner: unassigned | <負責者與 branch>
Resolution: <修復 SHA、驗證或不修理由>
```

