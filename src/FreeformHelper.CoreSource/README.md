# Temporary Core source consumption

`UiEventRunner.cs` and `manifest.json` are verbatim copies from Core commit
`f07d1568af2336229a488aca9b3e497c2531d7bb`.

- Source: `src/Nvt.Core/Threading/UiEventRunner.cs`
- SHA-256: `95a382e09250f1cc2f887935fb126dfe358a9eb4771d24f2a6fc5b2eeeed1c05`
- Byte length: `5480`
- Compile symbol: `NVT_CORE_SOURCE_CONSUMPTION`

This dependency-free project disables implicit usings so the canonical source
compiles without editing its bytes or suppressing diagnostics. It inherits the
repository's analyzer and central package policies and targets net10.0. Only the
UI and test assemblies receive access to the internal type. Do not add a direct
or transitive `Nvt.Core` dependency to the project compiling this copy.

Keep the source and manifest as LF. CRLF normalization excludes exactly these
two files; the formatter excludes only the source. Application debt and layering
scanners exclude only `src/FreeformHelper.CoreSource/UiEventRunner.cs`, whose raw
bytes are checked against the manifest by the architecture tests.

After the Core release that ships `UiEventRunner` (planned Core 0.9.0), remove this
project and the `NVT_CORE_SOURCE_CONSUMPTION` define, then pin the released Core
package. Remove the copy-specific integrity tests, scanner/formatter exclusions,
and LF attributes at the same time. Call sites keep `Nvt.Core.Threading`.
