# Issue tracker: GitHub

FreeformHelper 的規格與可執行 tickets 以 GitHub Issues 為唯一 tracker，repository 為 `Dennis40816/nvt-freeform-helper`。

owner 決定（2026-10-04；經 Commander 轉述）：只搬移經機密審查後可公開的舊 issue 至公開 repo，並改寫 `TODO.md` 與文件中對應連結；其餘舊 issue 在私有 `Dennis40816/FreeformHelper` 完成封存後仍保留在原 repo。這取代先前「舊 issue 不搬移、不改寫」的決定。舊 PR 仍不搬移、不改寫連結，封存後留在原私有 repo。As of 2026-10-05, only the parent spec was migrated (old issue 1 -> public issue 27); the 42 other open old issues are delivered child tickets and stay in the private repository. The bot's current issue permissions are described in `TODO.md` ("Bot issue permissions" under the 2026-10-05 owner decisions).

## Conventions

- 規格 issue：由 `to-spec` 發布，完成最高層級 test seam 決策後加上 `ready-for-agent`。
- 實作 ticket：由 `to-tickets` 拆分；每張 ticket 必須列出 scope、out of scope、acceptance criteria、test plan 與 blocker。
- 建立或修改 issue 前先搜尋相同 `R13.*` 或標題，避免重複。
- 優先使用 GitHub native sub-issues 與 issue dependencies；issue body 同時保留 `Part of #N`／`Blocked by: #N` 供人類直接閱讀。若 repository 不支援 native relationships，body 關係就是 fallback contract。
- Commit title 保持單一邏輯範圍，commit body 使用 `Refs #N`；完成 ticket 的 PR 才使用 `Closes #N`。
- Pull request body 應列出涵蓋的 issues、行為／契約影響、驗證命令與 golden evidence。
- From 2026-10-04, new PR titles and bodies default to English; already-open PRs are not rewritten. If `AGENTS.md` or `CONTRIBUTING.md` says otherwise, ask the owner in chat before changing either file.

## Pull requests as a triage surface

PR 不作為新需求入口；新需求先建 issue，再由 branch／commit／PR 回鏈。

## Completion 與歷史例外

- Native sub-issue／dependency links 是機器可查的關係；issue body 的 `Part of`／`Blocked by` 同時保留，供 reviewer 不開額外 UI 也能讀懂。
- `Closes #N` 表示完成意圖，但 GitHub 只有在 PR 進入 default branch 的整合流程中才可依平台規則自動關票。若 PR 先合入 release branch，必須在 default-branch integration PR 或 merge 後人工依 dependency 順序驗證 issue state。
- Ticket 建立前的 bootstrap commit，或已推送但 commit body 格式不完整的歷史 commit，不以 rewrite/force-push 修正；在 roadmap、PR 與 issue evidence明列例外。新 commit 一律使用獨立一行 `Refs #N`。

## Tooling

優先使用已連線的 GitHub connector 讀寫 issue／PR metadata；connector 未涵蓋的 labels、native dependencies、current-branch discovery 或 Actions logs 才使用 `gh`。
