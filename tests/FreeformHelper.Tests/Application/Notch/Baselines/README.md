# Full ordered Notch baselines

`NotchFullTableCharacterizationTests` compares every generated row and the complete
coverage audit against the `.txt` files in this directory. A missing file fails the
test; the test never creates a baseline on its own.

Record mode (only on the unchanged generator, from the repository root, after workspace
preparation with `-SkipNormalizeLineEndings`):

```powershell
$env:NFH_RECORD_NOTCH_BASELINE = '1'
dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug -p:UseAppHost=false --filter 'FullyQualifiedName~FreeformHelper.Tests.NotchFullTableCharacterizationTests'
Remove-Item Env:NFH_RECORD_NOTCH_BASELINE
```

Every case writes its file and then fails with "baseline recorded, rerun without
NFH_RECORD_NOTCH_BASELINE", so record mode can never produce a green run. Review and
commit all 42 files, then rerun the command with record mode unset. Record mode
overwrites expected files; never use it to approve refactor drift.

Files are UTF-8 without BOM with one final line ending. The repository's CRLF normalizer
(`scripts/tests/normalize-crlf.ps1`) and the `*.txt eol=crlf` rule turn the recorded LF into CRLF,
so the test reads CRLF as LF before comparing. Generated text never contains a raw CR (comment
CRs are escaped), so nothing else is normalized: no trimming, sorting or BOM removal.

Rows keep the existing literal snapshot prefix and append `NODE=...`; `AUDIT` and
every `GAP` follow. Backslash, pipe, CR, and LF in comments are escaped losslessly.
Rows, gaps, and contributing CAD lists retain their returned order.
