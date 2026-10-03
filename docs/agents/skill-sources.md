# Vendored agent skill sources

## mattpocock/skills

- Source: `https://github.com/mattpocock/skills`
- Pinned commit: `84fdeffd12f2ee307994d1eb6feb48173b6e0502`
- Upstream baseline: `v1.2.3` (tag commit `6acc160e4e0cd062dbbbd7a1b26ae92855edf07e`)
- Snapshot position: post-v1.2.3 (`2` commits ahead)
- License: MIT
- Installed paths:
  - `.agents/skills/setup-matt-pocock-skills`
  - `.agents/skills/to-spec`
  - `.agents/skills/to-tickets`
  - `.agents/skills/triage`
  - `.agents/skills/implement`
  - `.agents/skills/tdd`
  - `.agents/skills/diagnosing-bugs`
  - `.agents/skills/code-review`

Update review from `ed37663cc5fbef691ddfecd080dff42f7e7e350d`:

- 10 files changed across the installed snapshot (`22` insertions, `13` deletions).
- `diagnosing-bugs` and its HITL template now require secrets/auth material to be redacted rather than captured or echoed.
- `code-review` no longer assumes a specific sub-agent harness.
- `to-spec`, tracker templates, and review wording consistently use `spec` instead of the legacy `PRD` term.
- `tdd` adds a non-executable reference to the upstream `codebase-design` vocabulary; the installed skill set is otherwise unchanged.
- Upstream `LICENSE` is unchanged and remains MIT.

These directories are reviewed, repo-local snapshots rather than an automatically moving dependency. To update them, diff a newly pinned upstream commit against the commit above, review instruction and executable changes, update the snapshots and this provenance record in one dedicated commit, then rerun the repository lint gate.
