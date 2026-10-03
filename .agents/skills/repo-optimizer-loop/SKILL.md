---
name: repo-optimizer-loop
description: "Automate the repository optimization/refactor loop for FreeformHelper: scan the whole repo for performance, workflow, UI clarity, and code redundancy opportunities, convert findings into executable TODO.md steps, then iteratively implement each pending step with build/tests/commit/push until TODO has no actionable unchecked items. Use when the user asks for autonomous optimization, TODO-driven execution, or repo-wide refactor planning and delivery."
---

# Repo Optimizer Loop

Use this skill to run a strict TODO-driven optimization loop with minimal user intervention.

## Workflow Decision Tree
1. If `TODO.md` is missing optimization tasks or is stale: run **Step 1 Scan** then **Step 2 TODO Refresh**.
2. If `TODO.md` has unchecked actionable items: run **Step 3 Execution Loop**.
3. If `TODO.md` has no unchecked actionable items: run **Step 4 Closure**.

## Step 1: Repo Scan
Run a repo-wide scan and produce evidence-based findings.

Commands:
```powershell
pwsh .agents/skills/repo-optimizer-loop/scripts/repo_scan.ps1 -RepoRoot . -OutFile docs/guides/repo-refactor-scan-$(Get-Date -Format yyyy-MM-dd).md
```

Also verify stale behavior/doc contracts:
- `docs/reference/behavior-inventory.md`
- `docs/guides/settings-entry-matrix.md`
- `AGENTS.md`

## Step 2: TODO Refresh
Rewrite `TODO.md` into executable tasks only.

Rules:
- Keep unchecked items actionable and scoped.
- Split broad items into single-milestone steps.
- Each item must include:
  - scope/files,
  - acceptance commands,
  - done condition.
- Remove or archive already completed items to avoid drift.

Use helper:
```powershell
pwsh .agents/skills/repo-optimizer-loop/scripts/list_pending_todo.ps1 -TodoPath TODO.md
```

## Step 3: Execution Loop
Process pending TODO items one by one, top to bottom.

Per item loop:
1. Implement minimal scoped change.
2. Run mandatory verification:
   - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
   - targeted tests for touched area.
3. Update `TODO.md` immediately (`[ ]` -> `[x]`, add short completion note if needed).
4. Commit and push exactly one logical change set.
5. Continue with next unchecked item.

Stop only for:
- blocker,
- requirement conflict,
- high-risk design decision requiring user approval.

## Step 4: Closure
When no actionable unchecked TODO items remain:
1. Run full gate:
```powershell
pwsh scripts/tests/run-refactor-gate.ps1
```
2. Report:
- completed items,
- commit list,
- residual risks,
- deferred non-blocking opportunities.

## References
- Read `references/optimization-checklist.md` for scan dimensions and evidence expectations.
- Read `references/todo-task-template.md` when rewriting TODO entries.
