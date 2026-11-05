# MISRA F8 and F9 fix: sanitize Notch C row comments

Source audit: `cloud-notes/misra-generated-c-audit.md` on `origin/cloud/misra-generated-c-audit`, sections F8 (Rule 3.1 and syntax break) and F9 (Rule 3.2 line splicing).
Branch: `cloud/misra-f8-f9-fix`, started from `origin/main` at `5e48bec`.

## What changed

- `src/FreeformHelper.Application/Export/NotchFirmwareCExporter.cs:587` adds `SanitizeLineComment`. It is the one sanitizer for row comment text written after `//`:
  - every control character, including CR and LF, becomes a space, so a row always stays on one line;
  - a space is inserted inside `/*`, `*/` and `//`, so the comment holds no comment markers (Rule 3.1);
  - when the text ends in a backslash, optionally followed by spaces, those trailing backslashes and spaces are removed, so the line cannot splice into the next row (Rule 3.2). Other trailing spaces are kept, so normal comments are unchanged.
- `NotchFirmwareCExporter.cs:560` (`FormatV21NodeLine`) and `:577` (`FormatV22NodeLine`) now pass `node.Comment` through the sanitizer. The emitted table data (all initializer values) is unchanged.
- Today the projectors build comments only from numbers and enum names, so real exports are byte-identical before and after. Only hostile free-text comments change.

## Tests added

In `tests/FreeformHelper.Tests/Application/Notch/NotchTableExporterTests.cs`:

- `:691` `ExportAsCInitializer_HostileRowComment_StaysOnOneSafeCommentLine` (theory, 6 cases: v2.1 and v2.2, each with a newline comment, a `*/ /* ... //` comment, and a trailing-backslash comment). It exports a C file through `NotchTableExporter.ExportAsCInitializer` and asserts the comment lands on exactly one line, the text after the newline is on that line, the comment text has no `/*`, `*/` or `//`, and the line does not end with a backslash.
- `:738` `SanitizeLineComment_BreaksMarkersNewlinesAndTrailingBackslash` (theory, 4 cases) pins the sanitizer output for CR/LF, marker runs such as `///`, a trailing backslash, and backslash-space-backslash-newline at the end.

## What I ran

- `dotnet` is not installed in this cloud container (no `dotnet` on PATH, no `~/.dotnet`). I could not build or run the new tests. `pwsh` is also missing, so `prepare-ui-workspace.ps1`, `lint.ps1` and `repo-health.ps1 -Mode Verify` were not run.
- gcc check of the idea: I wrote two small C files with three `//` row comments (newline, `*/ /* //`, trailing backslash), once raw and once through a Python copy of the same sanitizer rules, and compiled them with `gcc -std=c99 -Wall -Wextra -Werror -c`. The raw file failed (`'newline' undeclared`). The sanitized file compiled cleanly and kept 3 initializers (checked with `_Static_assert`).

## Not verified

- The C# compiles and the 10 new test cases pass: not run (no dotnet).
- Analyzer and lint results, and the code-health ledgers (`eng/code-health/baseline.json`, `test-debt.json`): not run. The change adds one internal static method and one private static method, with no suppressions.
- In both current formats the comment is followed by more text (`srcD=..` in v2.1, `; NODE KEEP=..` in v2.2), so a trailing backslash in the row comment cannot reach the end of the line today. The sanitizer guards it anyway so a future format change cannot reintroduce F9.
- The `??/` trigraph (Rule 4.2, mentioned in the F9 fix note of the audit) is not handled; the task scope listed only CR/LF, comment markers and the trailing backslash.
- The audit also asks for a gcc-compiling guard test with hostile comments; the existing gcc tests were not extended.
