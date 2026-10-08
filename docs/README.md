# Docs Guide
Last updated: 2026-07-20

## New User Chinese Entry

If you are new to this project or new to operating the app, read this first:

1. `docs/guides/new-user-reading-guide.md`
   - Chinese reading route for new users, role split, and which documents to skip for now.

Shortest operating route:

1. `docs/guides/app-user-manual.md`
   - UI pages, Step1~Step5, and the basic Simulation / Export flow.
2. `docs/guides/settings-parameter-guide.md`
   - All Settings parameters, tuning advice, and cascade per-IC X/Y operations.
3. `docs/diagrams/notch-simulation/zh-TW/README.md`
   - Entry for the Chinese flow diagrams for Notch table and Simulation.

This section lists only documents that have a Chinese main text or an existing Chinese version. If a must-read document for new users exists only in English, add a Chinese version first before adding it here.

## Document Structure

- `docs/core/`
  - Core specifications, algorithm deep-dives, and workflow pipeline.
- `docs/diagrams/`
  - Mermaid flow diagrams; new users should start with `zh-TW`.
- `docs/guides/`
  - User manuals, settings guides, refactoring manuals, test categories, and reading guides.
- `docs/performance/`
  - Performance baselines, measurements, and optimization notes.
- `docs/reference/`
  - Canonical reference, external contracts, CLI design, and behavior inventory.
- `docs/generated/`
  - Tool-generated documents; not maintained by hand.
- `docs/archive/`
  - Historical documents that have left the main line but are kept for traceability.

## Maintainer Deep-Dive Route

This group is the reading order for taking over maintenance or debugging. It is not the shortest route for first-time app use.

1. `docs/reference/notch-system-reference.md`
   - The only canonical reference for the Notch pipeline, diff identity, simulation, and export contract.
2. `docs/core/freeform-helper-algorithms.md`
   - Full guide to the Freeform Helper main flow and Step1~Step5 algorithms, including the matching code entry points.
3. `docs/guides/app-user-manual.md`
   - Complete operating manual for general users and verifiers, from loading a project to Simulation / Export / Diagnostics.
4. `docs/core/notch-v21-v22-flow.md`
   - The only deep-dive for the Step5 flow, buckets, continuation, and export path.
5. `docs/diagrams/notch-simulation/zh-TW/README.md`
   - Entry to the Chinese Mermaid diagram library for Notch table calculation and Simulation validation.
6. `ROADMAP.md`
   - The only progress table: fix line, product features, and Core integration line, each item with target version, status, and PR link.
7. `docs/reference/refactor-contract.md`
   - 1.0.x refactor contract: zero-diff policy, Firmware Q7 contract, gates G0 to G6, golden update rules, and specifications for unfinished slices.
8. `docs/guides/post-1.0-tool-workbench-redesign-plan-2026-04-30.md`
   - Post-1.0 Tool Workbench redesign specification: Simulation completeness and the Coordinate artifact generation tool.
9. `docs/guides/refactor-playbook.md`
   - Standard workflow for refactoring and handover (branches, verification, commits).
10. `docs/guides/settings-parameter-guide.md`
   - SettingsWindow full parameter guide, tuning advice, and cascade per-IC X/Y operation instructions.
11. `docs/core/workflow-pipeline.md`
   - Step dependencies and invalidation rules.

## Common Supplementary Documents

- `docs/core/notch-v21-algorithm.md`
- `docs/core/notch-v22-algorithm.md`
- `docs/core/notch-validation-flow.md`
- `docs/core/notch-overall-flow-mermaid.md`
- `docs/guides/settings-entry-matrix.md`
- `docs/guides/test-categories.md`
- `docs/performance/perf-baseline-howto.md`
- `docs/performance/code-size-baseline-1.3.0.md`
- `docs/performance/regular-spatial-index-notes.md`
- `docs/reference/behavior-inventory.md`
- `docs/reference/cad-reg-numbering-reference.md`
- `docs/reference/notch-overlay-h5-design.md`
- `docs/reference/runtime-cli-plan.md`
- `example/BOE36.35/notch_export_v21_current.c`
- `example/BOE36.35/notch_export_v22_current.c`

## Do Not Start Here

- `docs/archive/`: Kept for history; does not represent the current main line. Old repo scan reports are collected in `docs/archive/repo-refactor-scans/`.
- `docs/guides/repo-refactor-scan-2026-06-26.md`: The latest scan record; suitable for tracking technical debt, not for onboarding.
- `docs/generated/`: Tool-generated; not maintained by hand.
- `docs/diagrams/notch-simulation/en/`: English diagram library; new users should start with `zh-TW`.
- `docs/guides/*redesign-plan*.md`, `docs/guides/post-1.0-*`: Design proposals or later plans, not operating manuals.

## Cleanup and Consolidation
- Deleted documents that no longer apply: `docs/non-notch-refactor-batches.md`
- Deleted documents that are fully covered by the canonical/deep-dive documents:
  - `docs/core/notch-algorithm-overview.md`
  - `docs/core/freeform-notch-spec.md`
  - `docs/code/notch_v21.md`
- Specification documents that were originally flat in the `docs/` root have been sorted by purpose into `core/guides/performance/reference/archive`.

## Maintenance Principles
- Only keep documents in `core/` or `guides/` that are still directly referenced in the current workflow.
- Content that can be regenerated by tools or programs should not be stored long-term in `docs/`.
- Old proposals that still need traceability must be moved to `docs/archive/` to avoid disturbing the main-line reading.

