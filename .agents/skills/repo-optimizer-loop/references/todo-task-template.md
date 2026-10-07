# Roadmap Task Template

Use this template for each unchecked task in `ROADMAP.md`. Put it under the line it belongs to: fix line, product features, or Core integration.

```markdown
- [ ] **<ID> <Title>** · Target: <version> · Status: <Not started | In progress | Blocked: reason | Waiting for owner> · PR: <link or ->
  - Goal: <single outcome>
  - Scope: `<file/path/a>`, `<file/path/b>`
  - Verification:
    - `<build/test command 1>`
    - `<build/test command 2>`
  - Done when: <observable condition>
```

## Splitting rule
- One task should be completable in one logical commit.
- If it needs multiple unrelated changes, split into multiple tasks.

## Ordering rule
1. P0 blocker/performance regressions
2. P1 workflow correctness and maintainability
3. P2 UI polishing and non-blocking cleanup
