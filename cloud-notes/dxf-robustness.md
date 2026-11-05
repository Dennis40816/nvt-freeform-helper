# DXF import robustness tests

Branch: `cloud/dxf-robustness` (from `origin/main` at `5e48bec`). Production code is not changed.

## What was added

One file: `tests/FreeformHelper.Tests/Application/Dxf/DxfPadImporterMalformedInputTests.cs` (49 test cases).
All samples are built in code (string lines or byte arrays). No binary fixture is checked in.

Path under test: `DxfPadImporter.Import(path, out catalog, options)`, the call `DxfImportService.ImportFromPath` makes,
plus the stream overload for the empty-file case.

Contract asserted for every input (`AssertCompleted`):

- The import ends within 10 s. It runs on a dedicated background thread, and a timeout fails the test and does not block the host.
- It returns a result, or throws an `InvalidDataException` with a message. Any other exception type is a failure.
- No pad has a non-finite vertex.

| Area | Cases |
| --- | --- |
| Empty / non-DXF | 0-byte file and stream, blank lines, text only, `0` / `0 EOF` only, binary-DXF sentinel with NUL bytes, 40 seeded random byte blobs |
| Truncation | cut at every line boundary of a valid document, cut mid-vertex (missing Y), odd line count, cut without SEQEND, cut inside a number (skipped, bug 1) |
| Unknown entities | SPLINE / 3DSOLID / HATCH / proxy / invented types carrying group 10/20, an unknown entity inside a legacy POLYLINE, empty entity type, unnamed block, INSERT with no or unknown block, mutually recursive blocks |
| Huge coordinates | 1E308, -1E308, double.MaxValue, 4.9E-324, a 30-digit integer (pass); 1E400, Infinity, INSERT scale overflow (skipped, bug 2) |
| NaN | `NaN`, `nan`, `-NaN` in LWPOLYLINE and legacy VERTEX: no NaN vertex reaches a pad, a triangle left with 2 valid vertices is dropped |
| Layer table | no TABLES section, TABLES without a LAYER table, nameless record, entity without group 8, duplicate and case-different records |
| Text encoding | invalid UTF-8 (Big5 bytes) in a layer name, UTF-8 BOM, UTF-16 LE/BE with BOM, NUL inside a value |
| Duplicates | same handle (group 5/330) on every entity, identical entities repeated, duplicate block names (case-insensitive) with repeated sections, duplicate layer records |

## Result

```
dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --filter "FullyQualifiedName~DxfPadImporterMalformedInputTests"
Passed: 37, Failed: 0, Skipped: 12
```

SDK 10.0.301 was installed into the session scratch directory. `scripts/fetch_core_packages.py` was run once so restore works
(`artifacts/` is git-ignored). The build has 0 errors and no new warning from the new file.
I ran each skipped test once with its `Skip` removed. Each failed the way its reason describes
(the two INSERT explosion tests hung past the 10 s limit and were killed by the timeout).

## Bugs found (tests kept skipped, production not fixed)

| # | Defect | Where | Skipped tests | Effect |
| --- | --- | --- | --- | --- |
| 1 | `ParseDouble` uses `double.Parse` with no handling. A non-numeric value (`abc`, empty, `-` from a file cut mid-number, a stray byte) throws a bare `FormatException` ("The input string 'abc' was not in a correct format"). The message names no group code or line. | `DxfPadImporter.Tokenization.cs` `ParseDouble` | `NonNumericCoordinate`, `EmptyCoordinateValue`, `TruncatedInsideNumber`, `InvalidUtf8InsideNumber` | The whole import aborts. `CadLoadUseCase` catches it (`ExceptionPolicy.IsRecoverable` only excludes `OutOfMemoryException`), so the user gets a load failure with an unclear message and loses the valid pads. |
| 2 | Only NaN is filtered. `Infinity` or an overflowing literal (`1E400`), and an INSERT scale that overflows, give infinite vertices. | `PolylineCommit.cs` `TryCommitPolyline`, `NormalizeVertices` | `NonFiniteCoordinateLiteral` (4 cases), `InsertScaleOverflow` | A pad with non-finite Bounds/Area reaches downstream code. |
| 3 | The vertex count is checked before the repeated closing vertex is removed. After removal a ring can have 2 vertices and `Polygon2` throws `ArgumentException`. Reproduced with a block triangle inserted at scale 1E-10 and with vertices A, B, A, A. | `PolylineCommit.cs` `TryCommitPolyline` | `BlockTriangleInsertedWithTinyScale`, `RingThatRepeatsTheFirstVertexTwiceAtTheEnd` | The import aborts for one degenerate entity. |
| 4 | `double.Parse(s, InvariantCulture)` allows a thousands separator, so `0,5` is read as 5 with no error. | `Tokenization.cs` `ParseDouble` | `CommaDecimalSeparator` | A file saved with a comma-decimal locale imports with wrong coordinates silently. |
| 5 | The INSERT column/row counts (group 70/71) have no cap. `70=2000000000, 71=2000000000` asks for 4e18 copies. | `EntityParsing.cs` `ParseInsert`, `InsertExpansion.cs` `ExpandInsert` | `InsertWithHugeArrayCounts` | Hang and memory growth. `OutOfMemoryException` is not recoverable in `ExceptionPolicy`, so the app can go down. |
| 6 | Only a block that contains itself is guarded. A 12-level chain where each block inserts the next one 10 times expands to 10^12 pads from under 1 KB. | `InsertExpansion.cs` `ExpandInsert` | `NestedInsertChainWithExponentialGrowth` | Same as 5. |
| 7 | (low) A layer table cut before `ENDTAB` loses its last record, because a record is added only on the next `LAYER` or `ENDTAB`. | `DxfLayerCatalogReader.cs` `ReadLayerDefinitions` | `LayerTableCutBeforeEndTab` | The last layer shows as not defined in the layer table. |

Bugs 1 to 3 would be covered by one rule: reject or skip the single bad entity and count it as a warning. There is no warning
channel today (`Import` returns only `CadPadSet`), so "skip with a warning" needs a small result model first.
That is a design decision for the owner and is not made here.

## Behavior pinned by passing tests worth knowing

- A NaN vertex in a polygon with 4 or more vertices is dropped silently. The pad is imported with the remaining vertices, so its shape changes. The tests only assert that no NaN remains.
- Unknown entities are counted in the layer catalog (`EntityCount`) but never contribute vertices.
- Invalid UTF-8 bytes in text become U+FFFD. The pad is still imported on a layer named with replacement characters.
- Mutually recursive blocks end (one expansion, no stack overflow).
- Pad ids stay unique when entity handles repeat. Handles are not read by the importer.

## Not run

- `./scripts/tests/lint.ps1` and `eng/core-health/repo-health.ps1 -Mode Verify` (no `pwsh` in the cloud container). I followed the repo rules by hand: no Task.Delay / Thread.Sleep, no `Assert` on elapsed time, temp path from `TestFiles`, file I/O through `TestFiles.WriteBytes`, no analyzer suppression, CRLF line endings. The test-debt ledger should not change, but this was not verified by the script.
- The full test suite. Only the new class was run.
- Nothing was run on real customer DXF (`example/` submodule not initialized in this container).

## For the local review

- Suggested order if bugs are adopted: 1+4 (one numeric parser with a clear error), 3, 2, then 5+6 (expansion cap), 7.
- When a bug is fixed, delete its `Skip` constant and the `Skip =` argument in the same commit so the test becomes a guard.
