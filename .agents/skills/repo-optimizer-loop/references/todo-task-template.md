# TODO Task Template

Use this template for each unchecked task:

```markdown
- [ ] **<ID> <Title>**
  - 目標：<single outcome>
  - 範圍：`<file/path/a>`、`<file/path/b>`
  - 驗證：
    - `<build/test command 1>`
    - `<build/test command 2>`
  - 完成定義：<observable condition>
```

## Splitting rule
- One task should be completable in one logical commit.
- If it needs multiple unrelated changes, split into multiple tasks.

## Ordering rule
1. P0 blocker/performance regressions
2. P1 workflow correctness and maintainability
3. P2 UI polishing and non-blocking cleanup
