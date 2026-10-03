param(
    [string]$TodoPath = "TODO.md",
    [switch]$AsJson
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if (-not (Test-Path $TodoPath)) {
    throw "TODO file not found: $TodoPath"
}

$lines = Get-Content $TodoPath
$section = ""
$pending = New-Object System.Collections.Generic.List[object]

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ($line -match '^##\s+(.+)$') {
        $section = $matches[1].Trim()
        continue
    }

    if ($line -match '^\s*-\s\[\s\]\s+(?:\*\*(.+?)\*\*|(.+?))\s*$') {
        $title = if (-not [string]::IsNullOrWhiteSpace($matches[1])) {
            $matches[1].Trim()
        }
        else {
            $matches[2].Trim()
        }

        $pending.Add([PSCustomObject]@{
                Index   = $pending.Count + 1
                Section = $section
                Title   = $title
                Line    = $i + 1
            })
    }
}

if ($AsJson) {
    $pending | ConvertTo-Json -Depth 4
    exit 0
}

if ($pending.Count -eq 0) {
    Write-Output "No unchecked TODO items found."
    exit 0
}

Write-Output "Pending TODO items: $($pending.Count)"
foreach ($item in $pending) {
    Write-Output ("{0}. [{1}] {2} (TODO.md:{3})" -f $item.Index, $item.Section, $item.Title, $item.Line)
}
