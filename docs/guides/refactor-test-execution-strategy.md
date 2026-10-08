# Refactor Test Layering Strategy

Last updated: 2026-07-20

## Goals
- Run a fixed order and a minimal regression set for every refactor, to reduce the risk of missed tests.
- Avoid parallel tests causing DLL locks (previously seen on Windows as `CS2012` / testhost lock).

## Fixed Order (Minimal Set)
1. `notch-golden`
2. `application`
3. `ui-core`
4. `smoke`

Corresponding commands:
```powershell
./scripts/tests/run-tests.ps1 -Group notch-golden
./scripts/tests/run-tests.ps1 -Group application
./scripts/tests/run-tests.ps1 -Group ui-core
./scripts/tests/run-tests.ps1 -Group smoke
```

## Recommended Gate (Refactor Milestones)
```powershell
./scripts/tests/run-refactor-gate.ps1
```

Default flow (fixed to run all test groups from 2026-10-02, see `ROADMAP.md` S15.003):
1. Check `example/` data (fetched, at the pinned commit, no uncommitted changes)
2. `lint`
3. `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
4. `notch-core` (already includes all golden categories)
5. `application`
6. `infrastructure`
7. `ui-core`
8. `ui-snapshots`
9. `uncategorized` (all test categories not explicitly listed)

`-IncludeNotchCore`, `-IncludeInfrastructure`, and `-IncludeUiSnapshots` can still be passed, but these groups are already the default behavior. Without permission to access `example/` data, add `-AllowMissingExampleData`.

Output:
- `build/test-gate/refactor-gate-summary.json`

## When to Run Extra Groups
The fixed gate already includes all groups below; this section is guidance for choosing groups when running only a single group with `run-tests.ps1`.

- `infrastructure`: when the change includes logging / console parser / runtime IPC text protocol.
- `notch-core`: when the change may affect the Notch generator/export/simulation/projection/settings/Runtime Query/VM data path.
- `ui-snapshots`: when UI visual structure, style tokens, or layout contracts have changed.
- Full `dotnet test`: only for very large cross-layer changes or before a release.

## Execution Principles

Run `pwsh -NoProfile -File ./scripts/tests/path-guard.ps1` to check tracked test code, JSON/CSV, project/settings files and text goldens, including an initialized private `example/` submodule (missing example coverage is reported); `-SelfTest` runs only synthetic samples. JSON strings are decoded before checking local path forms, the expanded `USERPROFILE`, and whole username path segments. Findings report only file, line or JSON path, and rule, never values; exit codes are 1 for findings or invalid configuration and 0 otherwise. The default `.github/path-guard.json` has an empty `exceptions` array; each exception requires an exact repository-relative `file`, `rule`, `reason`, `owner`, ISO `expiry` date (inclusive, UTC), and a `field` JSON path (for example, `$['mask']`) or `fingerprint` (SHA-256 of the UTF-8 decoded JSON string or text line without its newline); when both are supplied, both must match. This is a report-only prototype with no CI or `verify.ps1` integration.

- Run in priority order (do not open multiple `dotnet test` at the same time).
- If any group fails, stop and fix it. Do not run everything first and then go back.
- Before commit, at least ensure the minimal set (application/ui-core/smoke) is fully green.
