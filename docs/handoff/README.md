# Agent Handoff Protocol

This directory stores live handoff state for work across sessions or multiple people: who owns what, on which branch and head, which verifications are done, and which gates remain. Product behavior follows the contracts in `docs/reference/` and the current roadmap. Executable to-dos and status are maintained in `ROADMAP.md`; the handoff records do not copy a second backlog.

## Files and responsibilities

- `<version>.md`: version coordination board. Records the base, workstreams, owners, pending decisions, and cross-branch blockers.
- `<version>/<workstream-id>.md`: handoff record for a single workstream. Maintained by that workstream's owner.
- `bugs/ledger.md`: bug ledger index, currently empty. When a bug is found, add `bugs/BUG-<yyyymmdd>-<slug>.md`, then add that ID and link to the ledger. Use one file per bug to avoid parallel write conflicts.

## Required content of a handoff record

Each workstream record first states the following, so the next person does not have to guess the status from chat history:

1. **Outcomes and non-goals**: observable completion criteria and the scope that is explicitly not handled.
2. **Branch, worktree, and baseline**: base commit, current head, target branch. Use relative workspace identifiers, not absolute paths from private machines.
3. **Materials to read first**: applicable `AGENTS.md`, reference contracts, roadmap, TODOs/issues, and earlier verification evidence.
4. **Execution information**: known runtime/model settings, and the reason for the work assignment. If unknown, state that it is unknown.
5. **Acceptance and pending decisions**: expected checks, evidence locations, and items that need a decision from the owner.

## Checkpoint format

Each handoff appends a checkpoint at the end of the record, listing at least: date and summary; status (planned, local, verified, integrated, published); commit and head; actual commands, results, and their corresponding SHAs; modified and untracked files; review findings; unfinished gates, blockers, and who can answer them; and the next concrete action. Uncommitted changes cannot be read only from the Git history of another branch, so the record must state them explicitly.

To view a handoff record committed on another branch, use `git show <branch>:docs/handoff/<version>/<workstream-id>.md`. Handoff records report only the state that was actually verified. `verified` does not mean integrated or published.

## Bug ledger

When you find behavior that contradicts an existing contract, test, or document, or when a gate fails for a wrong reason, create one file under a bug ID. Mark unconfirmed leads as `suspected`.

Public handoff and bug records keep only shareable IDs, hashes, and path references. Do not write private `example/` content, credentials, or personal workstation paths into them.

## Suggested flow for adopting the template (not yet confirmed by the owner)

- During a handoff, record executable actions such as edits, local commits, pushes, PRs, and GitHub writes, the writable scope, and the required people and golden gates. The handoff record itself does not expand authorization; how to handle changes outside the scope is still to be confirmed.
- Bug files may use this field format:

```text
# BUG-<yyyymmdd>-<slug>: <title>
Status: suspected | open | fixing | fixed | wontfix | duplicate
Severity: P0 | P1 | P2 | P3
Found: <date, task, branch@sha>
Where: <path and line number, or command>
Observed: <actual behavior>
Expected: <expected behavior and the contract it is based on>
Evidence: <reproducible command and result, or code location>
Owner: unassigned | <owner and branch>
Resolution: <fix SHA, verification, or reason for not fixing>
```

