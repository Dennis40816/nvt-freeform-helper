# Pull request body skeleton

This file gives the headings and column names for a pull request body. Using the same names makes PRs easy to compare. No script or CI check reads these headings, so a PR is not blocked if it differs.

## Rules

- Write headings, column names and labels in English. This follows the English default in `docs/agents/issue-tracker.md`.
- Leave out a section that does not apply. Do not keep an empty heading.
- To quote the owner, put the original words in quotes or backticks and add an English meaning.
- Name the commit that a "Before" value was measured at. Use the full SHA of `main`.
- The `Review record` line is the last line of the review section. It holds the full head SHA that the independent review checked, and the verdict.

## Skeleton

```markdown
Approval tier: <A or B> (<one short reason>)

## Purpose

<What changes and why, in two or three sentences. Say if it changes no behavior.>

## What changes

- <One change per bullet.>

## Before and after

<Only when the owner sees a screen change. A table of before and after images, light and dark.>

## Behavior change (<owner decision, for example "owner Q55 approval, 10-15 08:49">)

<Only when behavior changes. A table of old and new behavior.>

## Ledger (before → after)

The Before column is the value on `main` at `<full SHA>`.

| Metric | Before | After |
| --- | --- | --- |
| <ledger entry> | <old value> | <new value> |

## Checks run

- <Command actually run, and its result.>
- <Say what only GitHub CI ran.>

## Review

<What the independent review checked and found.>

Review record: <full head SHA> accept

Refs #<issue>
```

## Notes

- The `Approval tier` line is optional. The "Merge Boundaries" section of `CONTRIBUTING.md` says which paths need owner approval.
- A PR that has no related issue writes `Refs: none` and gives the reason.
