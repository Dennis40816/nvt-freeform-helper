# Domain docs

FreeformHelper 採 single-context repository layout；`src/` 內的 Domain、Application、Infrastructure、UI 是架構分層，不是各自獨立的 bounded-context tracker。

## Before exploring

依序讀取：

1. `docs/generated/project-dependency-graph.md`
2. `docs/guides/refactor-roadmap-1.3.x.md` 與 `TODO.md`
3. 任務直接相關的 `docs/reference/` contract
4. 涉及 workflow／UI 時再讀 `docs/guides/refactor-playbook.md`

Notch 真實規格以 `docs/reference/notch-system-reference.md` 為入口；Runtime CLI 以 `docs/reference/runtime-cli-plan.md` 為契約。若文件與 production behavior 或 checked-in golden 衝突，ticket 必須明列證據與簽核 gate，不可自行選一邊覆寫。

## Vocabulary and decisions

- Issue title、test name 與 implementation 名稱沿用現有 reference docs 的術語。
- 若需新增 `CONTEXT.md` 或 ADR，只在實際解決 domain term／architecture decision 時建立，不預先產生空文件。
- 若提案與既有 contract 衝突，issue 必須直接標示衝突文件與需要的人員決策。
