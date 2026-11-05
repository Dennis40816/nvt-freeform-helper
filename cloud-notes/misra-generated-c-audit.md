# MISRA C:2012 audit of the generated notch C file

Date: 2026-11-05. Base: `main` at `5e48bec`. Scope: read-only audit. No product code was changed.

## Summary

- No **mandatory** rule is violated by the sampled output.
- Real output from today's generator has **4 required** findings (F1–F4) and **3 advisory** findings (F5–F7). One of the required findings is a documented deviation (F4).
- **3 required** findings (F8–F10) are latent. Today's generated comments and indices do not trigger them, but nothing in the generator prevents them.
- Two findings depend on the real FW `notch.h`, which this repo does not contain (F2 conditionally, F3). Check them against the FW tree.
- The table-data rules the task named are compliant: 7.2 `U` suffixes, 9.2/9.3/9.4 braces and array sizes, `const` on the tables and the table pointer, 11.9 `NULL`, 15.5 single exit and 15.7 `else` termination. See "Compliant checks" below.

## Generators of C text

Only one generator emits C. The rest only call it or pass its text on.

| Generator | Role |
| --- | --- |
| `src/FreeformHelper.Application/Export/NotchTableExporter.cs:70` `ExportAsCInitializer` | Public entry point. Emits the empty-table stub at `:85` and otherwise calls `NotchFirmwareCExporter.Export`. |
| `src/FreeformHelper.Application/Export/NotchFirmwareCExporter.cs` | Emits all of the C text: header, macros, typedefs, tables, dispatch table and functions (v2.1 and v2.2 layouts). |
| `src/FreeformHelper.Application/Services/NotchV22FirmwareProjector.cs:130` `FormatFlagsLiteral` | Emits the v2.2 `NHC_FLAGS` initializer expression. |
| `src/FreeformHelper.Application/Services/NotchV21FirmwareProjector.cs:278-303`, `NotchV22FirmwareProjector.cs:93,127` | Build the row comment text, which is written into a `//` comment. |
| `src/FreeformHelper.UI/Services/NotchExportService.cs`, `RuntimeQueryUseCase.cs` (`export-notch --format c-v21/c-v22`) | Callers only. They write the string to disk. |

The CSV export in `NotchTableExporter.cs:20-63` is not C.

## How the outputs were produced and checked

- The `example/` submodule is private and was not initialized in this cloud container, so the example golden C files could not be used.
- .NET SDK 10.0.301 was used, after `python scripts/fetch_core_packages.py`.
- A temporary xUnit test (deleted afterwards, not committed) called `NotchTableExporter.ExportAsCInitializer` on these inputs:
  - **v2.1**: 3 ICs; IC1 has ADD, SUB and NONE legs; IC2 has a single leg; IC3 is empty (`NULL` dispatch entry).
  - **v2.2**: the Release and Debug profiles, with negative ratios and `NHC_FLAG_CONTINUATION | NHC_FLAG_ANCHOR_OVERRIDE`.
  - **Empty table**.
  - Both versions used `NullValue = 65535` and `CadAllocation`.
  - Two rows carried deliberately hostile comments (`*/ /*` and an embedded newline) to test F8 and F9.
- The `notch.h` stub matches the stub that `NotchTableExporterTests.WriteNotchHeaderStub` uses, plus `INT32`, `<stddef.h>` and `extern` prototypes for the two public functions.
- The tools were:
  - `gcc -std=c99 -pedantic -Wall -Wextra -Wconversion -Wsign-conversion`;
  - `cppcheck 2.13.0 --std=c99 --addon=misra` (from apt). No rule-text file was available, so cppcheck reports rule IDs only.

Results:

- **gcc**:
  - Clean v2.1 and v2.2 output: 0 warnings, including `-Wconversion`.
  - Empty table: `ISO C forbids an empty translation unit`.
  - Hostile comment: hard compile error (`'newline' undeclared`).
- **cppcheck MISRA**: on every non-empty file it reports 8.9, 2.5 (×2) and 8.7. On v2.1 it also reports 10.1 (×4). It reports nothing else.

## Findings

Severity is the MISRA C:2012 category (Mandatory / Required / Advisory). Line numbers refer to `main` at `5e48bec`.

### Found in real output

#### F1. Rule 1.1 (Required): the empty-table output is an empty translation unit

- Generated:
  ```c
  // Notch table is empty.
  ```
  The whole file is this one line. The same applies to `// Notch table contains no supported v2.1/v2.2 rows.`
- Emitted by: `NotchTableExporter.cs:85` and `NotchFirmwareCExporter.cs:51`.
- Why: C99 6.9 requires at least one external declaration (gcc `-pedantic`: "ISO C forbids an empty translation unit"). The file also drops `FUNC_NHC_DiffCompensation`, so any FW build that calls it fails at link time.
- Fix: for an empty table, emit the normal file with every `USER_NHC_NODE_NUM_ICn` set to `(0u)`, a dispatch table of `{ NULL, 0u }` entries, and the same function block. Otherwise, refuse the export (throw) and show the error in the UI. Do not write a comment-only `.c` file.

#### F2. Rule 20.9 (Required): `#if` relies on macros the file does not check

- Generated:
  ```c
  #if (USER_SWITCH_NOTCH_COMPENSATION == FUNC_ENABLE)
  ```
- Emitted by: `NotchFirmwareCExporter.cs:130`.
- Why: if `notch.h` defines neither macro, both evaluate to `0`, the test is `0 == 0`, and the notch code is enabled silently. This violates 20.9 whenever either macro is undefined at that point. The generator cannot confirm that `notch.h` defines them.
- Fix: emit a guard before the `#if`:
  ```c
  #if !defined(USER_SWITCH_NOTCH_COMPENSATION) || !defined(FUNC_ENABLE)
  #error "notch.h must define USER_SWITCH_NOTCH_COMPENSATION and FUNC_ENABLE"
  #endif
  ```
  `#error` is allowed under 20.x. Rule 20.13 is satisfied.

#### F3. Rule 8.4 (Required): external functions defined without a visible declaration (conditional)

- Generated:
  ```c
  void FUNC_NHC_DiffCompensationByIc(UINT8 u8Ic)
  void FUNC_NHC_DiffCompensation(void)
  ```
- Emitted by: `NotchFirmwareCExporter.cs:476` and `:489`.
- Why: a compatible declaration must be visible where an external function is defined. The file relies on `notch.h` for it. The repo's own gcc test stub (`NotchTableExporterTests.cs:1490-1512`) does not declare either function, so the repo has never checked this.
- Fix: confirm that the FW `notch.h` declares both functions. Add them to the test stub and add a guard test that compiles with `-Werror=missing-prototypes`, so the dependency is enforced. If FW does not declare `FUNC_NHC_DiffCompensationByIc`, see F6.

#### F4. Rule 10.1 (Required): right shift of a signed operand (documented deviation)

- Generated:
  ```c
  s16Sum += (INT16)((((INT32)S_2D_DIFFAFTER->as16Buf[pstTable[u16j].NHC_1ST_IDX]) * (INT32)pstTable[u16j].NHC_1ST_RATIO) >> 7);
  ```
  cppcheck reports four occurrences.
- Emitted by: `NotchFirmwareCExporter.cs:375`, `:380`, `:390`, `:395`. Each has the comment `MISRA C:2012 Rule 10.1 deviation NHC-DEV-01` on the line above (`:374`, `:379`, `:389`, `:394`).
- Why: `>>` on an essentially signed operand. The result for a negative value is implementation-defined.
- Fix: either keep the deviation and make sure a deviation record `NHC-DEV-01` exists in the FW MISRA compliance file, or remove the need for it. To remove it, use `/ 128` (which rounds toward zero, so the result changes for negative values; say so in the PR), or shift the magnitude and reapply the sign. v2.2 has no shift and needs no deviation.

#### F5. Rule 8.9 (Advisory): dispatch table could be at block scope

- Generated:
  ```c
  static const ST_PRI_NHC_IC_TABLE_INFO castNHC_TABLE_BY_IC[USER_NHC_IC_NUM] =
  ```
- Emitted by: `NotchFirmwareCExporter.cs:328`.
- Why: only `FUNC_NHC_DiffCompensationByIc` uses it.
- Fix: emit it as `static const` inside `FUNC_NHC_DiffCompensationByIc`, or record an advisory deviation that keeps all tables in the "Local Global Variables" section to match the FW layout.

#### F6. Rule 8.7 (Advisory): `FUNC_NHC_DiffCompensationByIc` has external linkage

- Generated:
  ```c
  void FUNC_NHC_DiffCompensationByIc(UINT8 u8Ic)
  ```
- Emitted by: `NotchFirmwareCExporter.cs:476`.
- Why: inside this translation unit, only `FUNC_NHC_DiffCompensation` calls it. cppcheck only sees one file, so this finding is valid only if no other FW file calls it.
- Fix: if no FW caller exists, emit it as `static` with a prototype in the "Local Function Prototype" section, which is empty today (`:131-134`). If a FW caller exists, declare it in `notch.h` (see F3).

#### F7. Rule 2.5 (Advisory): unused macros

- Generated:
  ```c
  #define USER_NHC_NODE_NUM        (6u)
  #define castNHC_TABLE           castNHC_TABLE_IC1
  ```
- Emitted by: `NotchFirmwareCExporter.cs:153` and `:171` (v2.1), `:201` and `:217` (v2.2).
- Why: nothing in the file uses either macro. When IC1 has no nodes, `castNHC_TABLE` also points to a symbol that does not exist (see the IC-empty case: `castNHC_TABLE_IC1` is not emitted).
- Fix: remove both macros if FW does not read them (search the FW tree first, as the coding rules require). If FW reads them, keep them and record the advisory deviation. Also emit `castNHC_TABLE` only when IC1 has nodes.

### Latent: not triggered by today's generated comments and indices

#### F8. Rule 3.1 (Required) and syntax break: the row comment is written into a `//` comment without escaping

- Generated (hostile sample):
  ```c
  { 0, 100, 100, 0, NHC_TYPE_SUB, 22, NHC_DIFF_NONE, NHC_TYPE_NONE, 0 }, // SELF add/sub */ /* tricky srcD=0
  ```
  A comment that contains `\n` splits the row, and the next line becomes code. gcc fails with `'newline' undeclared`.
- Emitted by: `NotchFirmwareCExporter.cs:550` (v2.1 `// {9}`) and `:569` (v2.2 `// {7}`). The text comes from `NotchV21FirmwareProjector.cs:278-303` and `NotchV22FirmwareProjector.cs:93,127`.
- Why: `/*` inside a comment violates 3.1. A newline is a hard syntax error. Today the comments are built only from numbers and enum names (`NotchAlgorithmHelpers.cs:200-208`, `NotchTableGenerator.Generation.V22.cs:388`), so real output is clean. But `NotchTableRow.Comment` is a free string, and the exporter does not restrict it.
- Fix: one sanitizer for all generated comment text, applied in `FormatV21NodeLine` and `FormatV22NodeLine`. It replaces CR, LF and other control characters with a space, replaces `/*`, `*/` and `//` with `/ *`, `* /` and `/ /`, removes a trailing `\`, and replaces `??` with `?-?`. Add a guard test that exports a row with each of these characters and compiles the result with gcc.

#### F9. Rule 3.2 (Required): line splicing in a `//` comment

- Generated (constructed sample):
  ```c
  1u, // path C:\pads\
  ```
  Followed by the next row, which the splice consumes.
- Emitted by: same lines as F8.
- Why: a comment that ends in `\` joins the next table row into the comment. The array then has fewer initializers than its declared size, which also breaks 9.3. gcc reports this only as `-Wcomment`. The repo test uses `-Werror` and would catch it, but a FW toolchain without `-Wall` would compile it silently.
- Fix: the F8 sanitizer, which removes a trailing `\` and also covers the `??/` trigraph (4.2).

#### F10. Rule 10.3 (Required): diff indices are written without a range check

- Generated:
  ```c
  { <DestinationDiffIndex>, ..., <FirstDiffIndex>, ... }
  ```
  Each value is written as a raw `int` into a `UINT16` member.
- Emitted by:
  - v2.1: `NotchFirmwareCExporter.cs:551`, `:554`, `:557`;
  - v2.2: `:570`, `:572`, `:574`;
  - `FormatDiffLiteral` at `:582-591`.
- Why: ratios and percents are clamped (`NotchV21FirmwareProjector.cs:40-47`, `NotchV22FirmwareProjector.cs:118-123`), but indices are not. A negative index or one above 65535 is not representable in `UINT16`, so the constant would violate 10.3 and wrap silently. Today's indices come from the grid and are small.
- `NHC_DIFF_NONE` is clamped to `0..0xFFFF` (`:155`, `:203`), but the comparison at `:584` uses the unclamped `nullValue`. A negative `NullValue` would make `NHC_DIFF_NONE` `0x0000`, which collides with diff index 0. This is already characterized in `NotchNullValueCollisionCharacterizationTests`.
- Fix: one check in `NotchFirmwareCExporter.Export` that throws when any emitted index or `NullValue` is outside `0..65535`. Also reject a `USER_NHC_NODE_NUM_ICn` above 65535, because `u16NodeNum` is a `UINT16`.

### Conditional on the FW `notch.h`: check before release

These items are not violations in the repo's test setup. Check them once against the real FW `notch.h`:

- **Rule 5.6 (Required), typedef names unique**: the generated `.c` file defines `ST_PRI_NHC_TABLE_NODE_INFO` and `ST_PRI_NHC_IC_TABLE_INFO` (`NotchFirmwareCExporter.cs:173-184`, `:219-228`, `:235-239`). If `notch.h` also typedefs either name, this violates 5.6 and is a C99 constraint error. Fix: emit the typedefs only where they are owned. Either keep them in `notch.h`, or keep them here with a `#ifndef` guard macro that FW defines.
- **Rule 5.4 (Required), macro redefinition**: `NHC_TYPE_*` has `#ifndef` guards (`:156-164`), but `NHC_DIFF_NONE`, `NHC_FLAG_*` and `USER_NHC_*` are defined unconditionally (`:152-155`, `:200-208`). A different definition in `notch.h` is a constraint violation. The `#ifndef` fallback has a second risk: it silently keeps an FW value that may not match what the table assumes. Fix: emit one consistent policy. Either define everything here and add a `#error` check when FW already defined a name with a different value, or define nothing here and `#error` when the name is missing.

## Compliant checks

- **7.2 (Required)**: every macro constant that is unsigned carries `u` (`(3u)`, `(0xFFFFu)`, `(1u << 1)`, and `0x{X2}u` in `FormatFlagsLiteral`). The table initializers are non-negative signed constants assigned to unsigned members, which the 10.3 exception allows, and negative v2.2 ratios go into `INT8`. gcc `-Wconversion -Wsign-conversion` reports nothing.
- **9.2 / 9.3 / 9.4 (Required)**:
  - Every struct initializer is fully braced.
  - Every array size is the matching `USER_NHC_NODE_NUM_ICn` macro, equal to the number of rows emitted.
  - Empty ICs emit no zero-length array.
  - `= { 0 }` uses the 9.3 exception.
  - No element is initialized twice.
- **9.1 (Mandatory)**: all locals are written before they are read.
- **const correctness and 8.13**: tables and the dispatch table are `static const`, `pstTable` is `const ST_PRI_NHC_TABLE_NODE_INFO*`, and all tables have internal linkage (8.7 and 8.8 are satisfied for the tables).
- **11.9**: `NULL` is used, and `NotchFirmwareCSingleExitTests` guards this.
- **15.5**: single exit, also guarded by `NotchFirmwareCSingleExitTests`.
- **15.6 / 15.7**: every body is braced, and every `else if` chain ends with `else`.
- **14.2, 17.3, 17.4, 17.7**: all compliant.
- **5.1 / 5.2 / 5.3 / 5.5, 21.2**:
  - The longest external identifier is `FUNC_NHC_DiffCompensationByIc` (29 characters, which is within the 31 that C99 guarantees). The longest internal identifier is `FUNC_NHC_DiffCompensationOneTable` (33 characters, within 63).
  - `USER_NHC_NODE_NUM_IC1` and `USER_NHC_NODE_NUM_IC10` stay distinct.
  - No identifier shadows another, no macro name matches another identifier, and none is reserved.
- **20.1**: `#include` comes first. **Dir 4.10** (include guard) does not apply, because the generated file is a `.c` file, not a header.

## Suggested order of work

1. F8 and F9: one comment sanitizer plus a gcc guard test. This is the only finding that can break the build.
2. F1 and F2: the empty-table file shape and the `#error` guard. Note that both change the output.
3. F10: an index range check in the single export entry point.
4. F3 and the 5.4/5.6 conditional items: confirm against the FW `notch.h`, then pin the header contract in the gcc stub.
5. F4–F7: deviation records or small layout changes, after the owner decides.
