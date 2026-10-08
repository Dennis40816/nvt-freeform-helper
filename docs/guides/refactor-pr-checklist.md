# Refactor PR Checklist

## Core Contract
- [ ] The same user-visible action has been converged to a single entry (command / service / use-case).
- [ ] The same user-visible result has been converged to a single source-of-truth model, with no second-pass re-derivation.
- [ ] Side effects (selection / invalidate / focus / status) have been centralized in a single path.

## S11.52 Direct Mutation Gate
- [ ] The state model involved in this PR does not expose a new public mutable collection directly.
- [ ] If a collection is updated, the caller uses a class API / service API instead (no external assembly of side effects).
- [ ] The Direct state mutation hotspots from `repo scan` have been reviewed and marked with a convergence route.
- [ ] If this PR adds a new hotspot, a tracking item and definition of done have been created in `ROADMAP.md`.

## Verification
- [ ] `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj /p:UseAppHost=false`
- [ ] targeted tests (list filter):
- [ ] `./scripts/tests/lint.ps1 -UseNoAppHost`
- [ ] Before merge, additionally run: `./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost`

## Docs / Handoff
- [ ] `ROADMAP.md` status synced (added / completed / archived).
- [ ] Behavior changes synced to `docs/reference/behavior-inventory.md` (if applicable).
- [ ] PR description includes: single entry, result model, side effects, verification commands and results.
