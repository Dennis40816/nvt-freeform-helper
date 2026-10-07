# Refactor PR Checklist

## Core Contract
- [ ] 同一 user-visible action 已收斂到 single entry（command / service / use-case）。
- [ ] 同一 user-visible result 已收斂到 single source-of-truth model，沒有 second-pass re-derivation。
- [ ] side effects（selection / invalidate / focus / status）已在單一路徑集中。

## S11.52 Direct Mutation Gate
- [ ] 本 PR 涉及的 state model 沒有新增 public mutable collection 直接外露。
- [ ] 若有集合更新，caller 端改走類 API / service API（非外部拼裝 side effects）。
- [ ] `repo scan` 的 Direct state mutation hotspots 已檢視並標註 convergence route。
- [ ] 本 PR 若新增 hotspot，已在 `ROADMAP.md` 建立追蹤項與完成定義。

## Verification
- [ ] `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj /p:UseAppHost=false`
- [ ] targeted tests（列出 filter）:
- [ ] `./scripts/tests/lint.ps1 -UseNoAppHost`
- [ ] merge 前額外執行：`./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost`

## Docs / Handoff
- [ ] `ROADMAP.md` 狀態同步（新增/完成/封存）。
- [ ] 行為變更已同步 `docs/reference/behavior-inventory.md`（若適用）。
- [ ] PR 說明包含：單一入口、結果模型、side effects、驗證命令與結果。
