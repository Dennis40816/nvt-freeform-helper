# Branch, Version, and Release Governance

Status: Documentation for the 1.3.x branch and existing gates; release automation remains part of `TODO.md S15.005e`.

## Choose Versions by Product Impact

`docs/guides/refactor-roadmap-1.3.x.md` defines the outcomes, sequence, and exit criteria for 1.3.0～1.3.5, and `TODO.md` tracks progress. Do not skip the exit criteria of the preceding version; correctness work that requires output changes must not be mixed into zero-difference refactor/UI commits. `2.0.0 = 開始共用核心架構` (2.0.0 = start sharing the core architecture) is the owner's provisional goal from 2026-10-02, not yet a finalized version contract.

## Maintain a Single Version Identity

For the `VERSION` value and the repository-external test area, see `TODO.md` S15.005a. This repository cannot currently claim that the template mappings among `VERSION`, tag, package, manifest, and Catalog are in effect; the formal policy for version identity and release artifacts must be decided as part of `S15.005e`.

## Branch Authority and Work Direction

- `main` is the default branch and stores only released versions; the current minor-line trunk is `1.3.x`. Independent work uses `feature/<version>/<topic>`, with the corresponding trunk as the PR target; do not merge features directly into `main`.
- Work on 1.3.x first addresses the `S15.*` baseline fixes and alignment with the public template, then proceeds to unfinished `R13.*`; the owner has decided that `R13` can begin when only owner-only `S15` items remain.
- This repository has not yet completed the execution contract for release branches, tags, and release workflows; until `S15.005e` is complete, assess work completion using the current CI and roadmap gates, without citing the template release commands.

## Include Work in Versions and PRs

Each PR describes the outcome, associated issue/TODO, affected workflow/contract, and verification. Review and merge requirements follow the ["Merge boundaries" in the contribution guide](../../CONTRIBUTING.md#合併邊界owner-決定2026-10-02).

## Release Recovery and release notes

This repository's release workflow, rehearsal, recovery steps, and release notes format have not yet been implemented under `S15.005e`. Current work may report only completed local verification and CI evidence; do not treat the template tag recovery, signing, package, or release closure procedures as approved repository policy.

## Post-Release PR and Branch Cleanup

This repository has not yet decided the authorization and steps for bulk PR closure, remote branch deletion, and release merge-back; also see `TODO.md S15.005d` and `S15.005e`.
