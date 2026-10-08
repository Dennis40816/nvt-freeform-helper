# Domain docs

FreeformHelper uses a single-context repository layout. The Domain, Application, Infrastructure, and UI folders under `src/` are architecture layers, not separate bounded-context trackers.

## Before exploring

Read in this order:

1. `docs/generated/project-dependency-graph.md`
2. `ROADMAP.md` and `docs/reference/refactor-contract.md`
3. The `docs/reference/` contracts that are directly related to the task
4. `docs/guides/refactor-playbook.md` when the task involves workflow or UI

The entry point for the real Notch specification is `docs/reference/notch-system-reference.md`. The contract for the Runtime CLI is `docs/reference/runtime-cli-plan.md`. If a document conflicts with production behavior or with a checked-in golden, the ticket must list the evidence and the sign-off gate. Do not choose one side and overwrite the other on your own.

## Vocabulary and decisions

- Issue titles, test names, and implementation names follow the terms used in the existing reference docs.
- Create a `CONTEXT.md` or ADR only when it actually resolves a domain term or architecture decision. Do not create empty documents in advance.
- If a proposal conflicts with an existing contract, the issue must name the conflicting document directly and state which decision the team needs to make.
