# Branch, Version, and Release Governance

Status: Documentation for the 1.0.x branch and existing gates; release automation remains part of `ROADMAP.md S15.005e`. The version rules are in `ROADMAP.md`, section "Version rules".

## Choose Versions by Product Impact

`ROADMAP.md` holds the version order, target versions and progress on three lines: fixes, product features and Core integration. `docs/reference/refactor-contract.md` defines the gates and exit criteria for the handoff groups g1～g7. Do not skip the exit criteria of the preceding group; correctness work that requires output changes must not be mixed into zero-difference refactor/UI commits. NVT Core adoption starts in g4 (owner decision, 2026-10-05); this replaces the provisional 2026-10-02 goal that 2.0.0 starts the shared core architecture.

## Maintain a Single Version Identity

For the `VERSION` value and the repository-external test area, see `ROADMAP.md` S15.005a. This repository cannot currently claim that the template mappings among `VERSION`, tag, package, manifest, and Catalog are in effect; the formal policy for version identity and release artifacts must be decided as part of `S15.005e`.

## Branch Authority and Work Direction

- `1.0.x` is the default branch and the trunk. Its name follows the next customer release, `1.0.0`. `main` will hold customer releases only. Independent work uses `feature/<version>/<topic>`, with the corresponding trunk as the PR target; do not merge features directly into `main`.
- Work on 1.0.x first addresses the `S15.*` baseline fixes and alignment with the public template, then proceeds to unfinished `R13.*`; the owner has decided that `R13` can begin when only owner-only `S15` items remain.
- This repository has not yet completed the execution contract for release branches, tags, and release workflows; until `S15.005e` is complete, assess work completion using the current CI and roadmap gates, without citing the template release commands.

## Include Work in Versions and PRs

Each PR describes the outcome, associated issue/TODO, affected workflow/contract, and verification. Review and merge requirements follow the ["Merge boundaries" in the contribution guide](../../CONTRIBUTING.md#merge-boundaries-owner-decision-2026-10-02).

## Release Recovery and release notes

This repository's release workflow, rehearsal, recovery steps, and release notes format have not yet been implemented under `S15.005e`. Current work may report only completed local verification and CI evidence; do not treat the template tag recovery, signing, package, or release closure procedures as approved repository policy.

## Post-Release PR and Branch Cleanup

This repository has not yet decided the authorization and steps for bulk PR closure, remote branch deletion, and release merge-back; also see `ROADMAP.md S15.005d` and `S15.005e`.
