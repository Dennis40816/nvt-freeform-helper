# Issue tracker: GitHub

FreeformHelper's specifications and executable tickets use GitHub Issues as the only tracker. The repository is `Dennis40816/nvt-freeform-helper`.

Owner decision (2026-10-04; relayed by Commander): Only old issues that pass confidentiality review and are safe to make public will be migrated to the public repo. `TODO.md` and the matching links in the documents will be rewritten. The other old issues stay in the original private repo `Dennis40816/FreeformHelper` after it is archived. This replaces the earlier decision that old issues are not migrated and not rewritten. Old PRs are still not migrated and their links are not rewritten; they stay in the original private repo after archiving. As of 2026-10-05, only the parent spec was migrated (old issue 1 -> public issue 27); the 42 other open old issues are delivered child tickets and stay in the private repository. The bot's current issue permissions are described in `TODO.md` ("Bot issue permissions" under the 2026-10-05 owner decisions).

## Conventions

- Specification issue: published by `to-spec`. After the highest-level test seam decision is complete, add `ready-for-agent`.
- Implementation ticket: split by `to-tickets`. Each ticket must list scope, out of scope, acceptance criteria, test plan, and blocker.
- Before creating or changing an issue, search for the same `R13.*` or title to avoid duplicates.
- Prefer GitHub native sub-issues and issue dependencies. Keep `Part of #N` / `Blocked by: #N` in the issue body too, so people can read them directly. If the repository does not support native relationships, the body relationships are the fallback contract.
- Keep the commit title to one logical scope. Use `Refs #N` in the commit body. Only a PR that completes a ticket uses `Closes #N`.
- The pull request body should list the covered issues, behavior or contract impact, verification commands, and golden evidence.
- From 2026-10-04, new PR titles and bodies default to English. Already-open PRs are not rewritten. If `AGENTS.md` or `CONTRIBUTING.md` says otherwise, ask the owner in chat before changing either file.

## Pull requests as a triage surface

PRs are not an entry point for new requirements. New requirements start as issues, and branches, commits, and PRs link back to them.

## Completion and historical exceptions

- Native sub-issue and dependency links are machine-readable relationships. The `Part of` and `Blocked by` lines in the issue body are kept too, so reviewers can read them without opening extra UI.
- `Closes #N` shows completion intent. GitHub only closes issues automatically under its platform rules when a PR enters the default branch integration flow. If a PR merges into a release branch first, a default-branch integration PR or merge must be used, or issue state must be checked by hand in dependency order.
- Bootstrap commits made before a ticket exists, and historical commits already pushed with incomplete commit body format, are not fixed by rewrite or force-push. List these exceptions in the roadmap, PRs, and issue evidence. New commits always use a separate single line `Refs #N`.

## Tooling

Prefer the connected GitHub connector to read and write issue and PR metadata. Use `gh` only for labels, native dependencies, current-branch discovery, or Actions logs that the connector does not cover.
