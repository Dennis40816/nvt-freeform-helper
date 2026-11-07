# Test access to non-public code: numbers on main (2026-11-08)

Conclusion: the tests no longer use `BindingFlags.NonPublic`. They still reach non-public members in two other ways: `[UnsafeAccessor]` helpers and string-name reflection on properties. This page lists each number, its value, and the command that measures it.

Measured on `origin/main` at `41f4ec8`. Run every command from the repository root in PowerShell. Re-measure before you quote a number, because other pull requests change it.

## Numbers

| # | What is counted | Value | Command |
|---:|---|---:|---|
| 1 | Test files that use `BindingFlags.NonPublic` | 0 | `@(Get-ChildItem tests -Recurse -Filter *.cs \| Select-String -Pattern 'BindingFlags\.NonPublic' -List).Count` |
| 2 | Calls that look up a member by string name (`GetField`, `GetMethod`, `GetProperty`, `GetEvent`, `GetConstructor`, `GetNestedType`) | 370 in 31 files | `Get-ChildItem tests -Recurse -Filter *.cs \| Select-String -Pattern '\.(GetField\|GetMethod\|GetProperty\|GetEvent\|GetConstructor\|GetNestedType)\('` |
| 3 | `[UnsafeAccessor]` attributes in tests | 89 in 12 files | `Get-ChildItem tests -Recurse -Filter *.cs \| Select-String -Pattern '\[UnsafeAccessor\('` |
| 4 | Distinct member names those attributes pin | 85 | `Select-String -Pattern 'UnsafeAccessor\([^)]*Name = "([^"]+)"' -AllMatches`, then count the distinct `Groups[1].Value` |
| 5 | Banned-symbol rule for the test project | `BindingFlags.NonPublic` is banned | `Select-String -Path eng/core-health/BannedSymbols.Tests.txt -Pattern 'NonPublic'` |

## What the numbers mean

- Number 1 is 0 because row 5 bans the flag in the test project. The compiler stops a new use.
- Number 2 is almost all `GetProperty`. These calls read public view-model properties by name in property tests. They use `BindingFlags.Public` or no flags, so they do not reach private members. The remaining calls are 4 `GetMethod` (test method names in `HeadlessSessionGuardTests`) and 1 `GetField`. None of them passes `NonPublic`, so they can only find public members. Nothing reads a private name through reflection.
- Numbers 3 and 4 are the real private-name coupling. Each attribute names a private field or method of production code, and a rename breaks the test build, not only a test. No rule limits this today.

## Where the `[UnsafeAccessor]` attributes are

| Attributes | File |
|---:|---|
| 39 | `tests/FreeformHelper.Tests/TestInfrastructure/PadCanvasPrivateAccess.cs` |
| 24 | `tests/FreeformHelper.Tests/TestInfrastructure/FreeformHelperViewModelPrivateAccess.cs` |
| 9 | `tests/FreeformHelper.Tests/TestInfrastructure/NotchTableGeneratorPrivateAccess.cs` |
| 8 | `tests/FreeformHelper.Tests/TestInfrastructure/FreeformHelperViewPrivateAccess.cs` |
| 2 | `tests/FreeformHelper.Tests/UI/Services/ManualFreeformTaggingTests.cs` |
| 1 each | `SimulationWorkspaceViewModelPrivateAccess.cs`, `SettingsGeneralSectionViewPrivateAccess.cs`, `RuntimeQueryIpcServerPrivateAccess.cs`, `RuntimeQueryCommandLinePrivateAccess.cs`, `NotchExportSelectionWindowPrivateAccess.cs`, `DxfOverlapReportWindowPrivateAccess.cs`, `DispatcherPrivateAccess.cs` (all in `TestInfrastructure`) |

By kind: 36 fields, 1 static field, 42 methods, 8 static methods, 2 constructors.

Ten of the twelve files are helper classes named `*PrivateAccess`. Tests call the helper, so a rename touches one file. That is the reason the count is not larger in the test classes.

## Is a test seam for moving fields still needed?

The roadmap items that group always-changing fields into records (ST3) and that move canvas selection state (ST5) change private fields of the view model and the canvas. The tests reach 24 view-model names and 39 canvas names through the helpers above. A move breaks those 63 attributes.

- The helpers keep the break in 2 files.
- A public read-only seam on the view model and the canvas would remove the coupling. It has a cost: production code gets members that only tests call, which the test rules do not allow.
- This page does not decide it. It gives the numbers a decision needs.

## How to keep the numbers honest

- Re-run the five commands when a pull request adds a `*PrivateAccess` helper or a new `[UnsafeAccessor]`.
- A drop in numbers 3 and 4 is progress. A rise means a new test pins a private name.
- No test in the repository checks numbers 2 to 4 today. A ratchet test can pin them in the same way as the other counters in `tests/FreeformHelper.Tests/Architecture/debt-baseline.json`. That is a separate change.
