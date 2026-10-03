# Logging Policy

Last updated: 2026-02-12

## Scope
- Applies to UI / Application / Infrastructure logs.
- Goal: keep runtime signals actionable and reduce noise.

## Level Policy
- `Debug`
  - High-frequency, state-churn, interaction details.
  - Examples: selection delta, temporary UI states, skip reasons that are expected.
- `Info`
  - User-visible stage transitions and successful operation outcomes.
  - Examples: import/load/save/rebuild/match/export started or finished, produced counts.
- `Warn`
  - Unexpected but recoverable conditions.
  - Examples: fallback path, no rows generated when export requested, missing dialog handler.
- `Error`
  - Operation failure or exception path.
  - Must include exception object when available.

## Format Contract
- File log and in-app console share the same semantic fields:
  - `time | level | logger/source | message`
- File target remains full-fidelity with exception stack on new line.
- In-app console is rendered through `AppLogFormatter` for a single source of truth.

## Current Rules (implemented)
- Default runtime rule: `Info+` to file + in-app console.
- Selection spam remains `Debug` and is suppressed from default output.
- Notch flow emits stage logs:
  - generation start (mode/versions/threshold)
  - generated row count
  - export selection keep/cancel status
  - notch detail open context
