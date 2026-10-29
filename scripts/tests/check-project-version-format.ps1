Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "project-version-format.ps1")
$pattern = Get-ProjectVersionPattern
$crlf = "`r`n"

$mustAccept = @(
    @{ Name = 'customer release'; Value = '1.0.0' + $crlf },
    @{ Name = 'development version'; Value = '1.0.0-g.4' + $crlf },
    @{ Name = 'first development version'; Value = '1.0.1-g.1' + $crlf },
    @{ Name = 'two digit parts'; Value = '10.20.30-g.12' + $crlf },
    @{ Name = 'zero parts'; Value = '0.0.0-g.1' + $crlf }
)

$mustReject = @(
    @{ Name = 'LF ending'; Value = "1.0.0-g.4`n" },
    @{ Name = 'no line ending'; Value = '1.0.0-g.4' },
    @{ Name = 'two lines'; Value = '1.0.0-g.4' + $crlf + '1.0.0' + $crlf },
    @{ Name = 'leading space'; Value = ' 1.0.0-g.4' + $crlf },
    @{ Name = 'trailing space'; Value = '1.0.0-g.4 ' + $crlf },
    @{ Name = 'only two parts'; Value = '1.0-g.4' + $crlf },
    @{ Name = 'leading zero in a part'; Value = '01.0.0-g.4' + $crlf },
    @{ Name = 'N is zero'; Value = '1.0.0-g.0' + $crlf },
    @{ Name = 'leading zero in N'; Value = '1.0.0-g.04' + $crlf },
    @{ Name = 'N is missing'; Value = '1.0.0-g.' + $crlf },
    @{ Name = 'dot is missing'; Value = '1.0.0-g4' + $crlf },
    @{ Name = 'upper case suffix letter'; Value = '1.0.0-G.4' + $crlf },
    @{ Name = 'other suffix'; Value = '1.0.0-rc.1' + $crlf },
    @{ Name = 'suffix then more text'; Value = '1.0.0-g.4-x' + $crlf },
    @{ Name = 'build metadata'; Value = '1.0.0-g.4+abc' + $crlf }
)

foreach ($sample in $mustAccept) {
    if (-not ($sample.Value -cmatch $pattern)) { throw "Project version pattern rejected $($sample.Name)." }
}

foreach ($sample in $mustReject) {
    if ($sample.Value -cmatch $pattern) { throw "Project version pattern accepted $($sample.Name)." }
}

Write-Host "[check-project-version-format] PASS: $($mustAccept.Count) accepted and $($mustReject.Count) rejected samples."
