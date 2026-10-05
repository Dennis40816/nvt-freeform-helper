# Beginner Chinese Reading Guide

Last updated: 2026-05-23

This document answers two questions:

1. Which documents to read first when taking over FreeformHelper for the first time.
2. Which documents are primarily in Chinese or already have a Chinese version and can safely be included in the beginner reading path.

## Shortest Path

If you are simply using the app or validating a panel for the first time, these three documents are enough to start:

| Order | Document | Audience | What you should know after reading |
| --- | --- | --- | --- |
| 1 | `docs/guides/app-user-manual.md` | Users operating the app for the first time | UI pages, Step1~Step5, and the basic Simulation / Export workflow. |
| 2 | `docs/guides/settings-parameter-guide.md` | People who need to tune parameters | What each Settings parameter does, how to adjust it, and how to use cascade per-IC X/Y. |
| 3 | `docs/diagrams/notch-simulation/zh-TW/README.md` | People who need to understand the Notch / Simulation workflow | Notch table and Simulation are two separate lines of responsibility, and which diagram to open. |

All three are primarily in Chinese; the third is the `zh-TW` version of the diagram library.

## Algorithm Mermaid Links

The algorithm Mermaid diagrams are currently organized in Mermaid documents within the repo; beginners should start with the Chinese diagram library and do not need to read the English version first.

| Purpose | Mermaid document | Description |
| --- | --- | --- |
| Notch / Simulation overview | `docs/diagrams/notch-simulation/zh-TW/README.md` | Current diagram library entry point, distinguishing the two lines of responsibility for Notch table and Simulation. |
| Notch table module map | `docs/diagrams/notch-simulation/zh-TW/notch-table.md` | Layer diagram for row identity, candidate, compensation, and output projection. |
| Identity contract | `docs/diagrams/notch-simulation/zh-TW/notch-table-identity.md` | How Regular / CAD / SeeRegular converge into a single snapshot. |
| Candidate assembly | `docs/diagrams/notch-simulation/zh-TW/notch-table-candidate.md` | The process for building CadAllocation candidate buckets. |
| Compensation + target | `docs/diagrams/notch-simulation/zh-TW/notch-table-compensation.md` | Responsibility boundaries for ToRegular / ToFull / Stage3 / target allocation. |
| Output projection | `docs/diagrams/notch-simulation/zh-TW/notch-table-output.md` | v2.2 canonical, v2.1 compatibility, CAD output grid, and handoff. |
| Simulation module map | `docs/diagrams/notch-simulation/zh-TW/simulation.md` | Simulation source, apply, audit, and UI/export/replay layers. |
| Overall flow | `docs/core/notch-overall-flow-mermaid.md` | Overview diagram for maintainers; if it differs from the canonical reference, `notch-system-reference.md` takes precedence. |

## Reading by Role

| Role | Recommended documents | Notes |
| --- | --- | --- |
| General user / validator | `app-user-manual.md` -> `settings-parameter-guide.md` -> `zh-TW` diagram README | No need to read the algorithm deep-dive first. |
| Adjusting Settings for the first time | `settings-parameter-guide.md` -> `settings-entry-matrix.md` | The former gives operating and tuning guidance; the latter gives settings entry points and the single source contract. |
| Taking over workflow maintenance for the first time | `workflow-pipeline.md` -> `settings-entry-matrix.md` -> `refactor-playbook.md` | Understand Step dependencies and invalidation first, then read the refactoring rules. |
| Debugging Notch / Export for the first time | `notch-system-reference.md` -> `freeform-helper-algorithms.md` -> `notch-v21-v22-flow.md` | `notch-system-reference.md` is the current source of truth; the others are deep-dives. |
| Looking at Simulation for the first time | `docs/diagrams/notch-simulation/zh-TW/README.md` -> the Simulation section of `notch-system-reference.md` | Look at the diagrams first, then compare them with the canonical reference. |
| Preparing to change code for the first time | `refactor-playbook.md` -> `workflow-pipeline.md` -> `behavior-inventory.md` | This path is for maintainers rather than an operating manual. |

## Chinese Version Status

The beginner reading path should include only documents that are primarily in Chinese or already have a Chinese version. The following Chinese documents can currently be included in the beginner reading path:

| Document | Chinese status | Purpose |
| --- | --- | --- |
| `docs/guides/new-user-reading-guide.md` | Primarily Chinese | Beginner entry point and reading paths. |
| `docs/guides/app-user-manual.md` | Primarily Chinese | App user manual. |
| `docs/guides/settings-parameter-guide.md` | Primarily Chinese | Settings parameter descriptions and tuning guidance. |
| `docs/diagrams/notch-simulation/zh-TW/README.md` | Chinese version | Notch / Simulation diagram library entry point. |
| `docs/core/notch-overall-flow-mermaid.md` | Chinese explanations + Mermaid | Algorithm overview for maintainers. |
| `docs/reference/notch-system-reference.md` | Primarily Chinese | Notch pipeline / Simulation / Export canonical reference. |
| `docs/core/freeform-helper-algorithms.md` | Primarily Chinese | Step1~Step5 algorithm deep-dive. |
| `docs/core/notch-v21-v22-flow.md` | Primarily Chinese | Step5 / v2.1 / v2.2 export flow deep-dive. |
| `docs/core/workflow-pipeline.md` | Primarily Chinese | Step dependencies and invalidation rules. |
| `docs/guides/settings-entry-matrix.md` | Primarily Chinese | Settings entry points and single source matrix. |
| `docs/guides/refactor-playbook.md` | Primarily Chinese | Maintenance and refactoring workflow. |
| `docs/reference/behavior-inventory.md` | Primarily Chinese | Inventory of current behavior and entry points for maintainer reference. |

Before adding an English document to the "required reading for beginners" in the future, a Chinese version must first be provided or the document must be changed to be primarily in Chinese.

## Documents to Skip for Now

The following documents are not beginner entry points unless you are investigating a specific historical or engineering issue:

- `docs/archive/`: Historical records that do not represent the current mainline; old repo scan reports are collected in `docs/archive/repo-refactor-scans/`.
- `docs/guides/repo-refactor-scan-2026-06-26.md`: The latest repo scan record, useful for tracking technical debt but unsuitable as an introduction.
- `docs/generated/`: Tool-generated and not maintained manually.
- `docs/diagrams/notch-simulation/en/`: English diagram library; beginners should start with `zh-TW`.
- `docs/guides/settings-overview-redesign-plan-*.md`, `post-1.0-*`: Design proposals or future plans, not operating procedures.

## Maintenance Rules

- `docs/README.md` is an entry index only and does not contain long-form content.
- Beginner documents should preferably be placed in `docs/guides/` and registered in this file and `docs/README.md`.
- If a topic has both Chinese and English versions, the beginner entry in README links only to the Chinese version.
- Historical documents must remain in `docs/archive/` or be clearly marked as outside the current mainline.
