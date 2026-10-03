# Repo Refactor Scan Template

> 用途：每次執行 repo scan 時，統一輸出格式，確保 S11.52 要求的 direct state mutation gate 不會漏掉。

## Metadata
- Generated:
- Branch:
- Commit:
- Scope:

## File Counts
- tracked files:
- src:
- tests:
- docs:
- scripts:

## Line Count Hotspots
- top files by line count:
- 風險摘要：
- 本輪是否處理：

## Unique Path Audit
- acceptable multi-entry / single-path:
- unacceptable multi-path re-derivation:
- 本輪收斂項目：
- 尚未收斂項目：

## Direct State Mutation Hotspots
- hotspot count:
- category summary:
- resolved this round:
- unresolved with convergence route:

### Top Hotspots
- file:
  - snippet:
  - convergence route:
  - owner:
  - target milestone:

## Build Gate
- command:
- status:
- log:

## Test Gate
- command:
- status:
- scope rationale:

## Lint Gate
- command:
- status:
- log:

## TODO Sync
- 新增 TODO:
- 更新 TODO:
- 已完成項目封存:

## PR Checklist Link
- 使用 checklist: `docs/guides/refactor-pr-checklist.md`
- direct mutation hotspot 已逐項對應 convergence route：`Yes/No`
