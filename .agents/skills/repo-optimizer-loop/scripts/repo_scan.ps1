param(
    [string]$RepoRoot = ".",
    [int]$TopN = 25,
    [string]$OutFile = "",
    [switch]$SkipLint,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = (Resolve-Path $RepoRoot).Path
Push-Location $root
try {
    function Get-TrackedFiles {
        param(
            [string[]]$Paths = @()
        )

        $gitArgs = @("ls-files")
        if ($Paths.Count -gt 0) {
            $gitArgs += "--"
            $gitArgs += $Paths
        }

        try {
            $output = @(git @gitArgs)
            if ($LASTEXITCODE -eq 0 -and $output.Count -gt 0) {
                return @($output | Where-Object { Test-Path -LiteralPath $_ })
            }
        }
        catch {
        }

        $searchRoots = if ($Paths.Count -gt 0) { $Paths } else { @(".") }
        $files = foreach ($candidate in $searchRoots) {
            if (-not (Test-Path -LiteralPath $candidate)) {
                continue
            }

            $item = Get-Item -LiteralPath $candidate
            if ($item -is [System.IO.FileInfo]) {
                $item.FullName
                continue
            }

            Get-ChildItem -LiteralPath $candidate -Recurse -File | ForEach-Object { $_.FullName }
        }

        return @($files | ForEach-Object {
            try {
                [System.IO.Path]::GetRelativePath($root, $_)
            }
            catch {
                $_
            }
        })
    }

    function Resolve-GateFailureKind {
        param(
            [string]$Message
        )

        if ([string]::IsNullOrWhiteSpace($Message)) {
            return "unknown"
        }

        $normalized = $Message.ToLowerInvariant()
        $environmentMarkers = @(
            "being used by another process",
            "failed to run",
            "resourceunavailable",
            "access is denied",
            "cannot find path",
            "could not find",
            "standardoutputencoding is only supported",
            "program '"
        )

        foreach ($marker in $environmentMarkers) {
            if ($normalized.Contains($marker)) {
                return "environment"
            }
        }

        return "code"
    }

    function New-GateResult {
        param(
            [string]$Name
        )

        return [ordered]@{
            name = $Name
            status = "skipped"
            failureKind = ""
            message = ""
            logPath = ""
            command = ""
        }
    }

    function Invoke-GateCommand {
        param(
            [Parameter(Mandatory = $true)]
            [System.Collections.IDictionary]$Result,
            [Parameter(Mandatory = $true)]
            [scriptblock]$Action
        )

        try {
            & $Action
            $Result.status = "ok"
        }
        catch {
            $Result.status = "failed"
            if ([string]::IsNullOrWhiteSpace($Result.message)) {
                $Result.message = $_.Exception.Message
            }
            if ([string]::IsNullOrWhiteSpace($Result.failureKind)) {
                $Result.failureKind = Resolve-GateFailureKind -Message $Result.message
            }
        }
    }

    $allTracked = @(Get-TrackedFiles)
    $srcCount = @($allTracked | Where-Object { $_ -match '^src[\\/]' }).Count
    $testsCount = @($allTracked | Where-Object { $_ -match '^tests[\\/]' }).Count
    $docsCount = @($allTracked | Where-Object { $_ -match '^docs[\\/]' }).Count
    $scriptsCount = @($allTracked | Where-Object { $_ -match '^scripts[\\/]' }).Count

    $scanFiles = @(Get-TrackedFiles -Paths @("src", "tests", "docs")) |
        Where-Object { $_ -notmatch '(^|[\\/])(bin|obj|build)([\\/]|$)' } |
        Where-Object { $_ -match '\.(cs|axaml|md)$' }

    $hotspotsRaw = foreach ($file in $scanFiles) {
        [PSCustomObject]@{
            Path  = $file
            Lines = (Get-Content $file).Count
        }
    }
    $hotspots = $hotspotsRaw | Sort-Object Lines -Descending | Select-Object -First $TopN

    $stateMutationScanFiles = @(Get-TrackedFiles -Paths @("src")) |
        Where-Object { $_ -match '\.cs$' } |
        Where-Object { $_ -notmatch '(^|[\\/])(bin|obj|build)([\\/]|$)' }
    $mutableCollectionPropertyPattern = [regex]'public\s+[^;=\(\)]*\b(List|Dictionary|HashSet|ObservableCollection|ICollection|IList|IDictionary|ISet)<[^>]+>\s+\w+\s*\{\s*get;\s*(set;)?'
    $stateMutationHotspots = @(
        foreach ($file in $stateMutationScanFiles) {
            $lines = Get-Content -LiteralPath $file
            for ($index = 0; $index -lt $lines.Count; $index++) {
                $line = $lines[$index]
                if (-not $mutableCollectionPropertyPattern.IsMatch($line)) {
                    continue
                }

                if ($line -match 'ReadOnlyObservableCollection') {
                    continue
                }

                $category = if ($line -match '\bset;') {
                    "PublicSettableMutableCollectionProperty"
                }
                else {
                    "PublicMutableCollectionProperty"
                }
                $convergeRoute = switch ($category) {
                    "PublicSettableMutableCollectionProperty" {
                        "Restrict setter and expose immutable/read-only snapshot; apply changes through dedicated API/use-case."
                    }
                    default {
                        "Keep private mutable backing collection and expose ReadOnlyObservableCollection/IReadOnlyList facade."
                    }
                }

                [PSCustomObject]@{
                    Path = $file
                    Line = $index + 1
                    Category = $category
                    Snippet = $line.Trim()
                    ConvergeRoute = $convergeRoute
                }
            }
        }
    )
    $stateMutationTop = @($stateMutationHotspots | Sort-Object Path, Line | Select-Object -First ([Math]::Max($TopN, 40)))
    $stateMutationCategorySummary = @($stateMutationHotspots | Group-Object Category | Sort-Object Count -Descending)

    $logDir = Join-Path $root "build/logs"
    if (-not (Test-Path -LiteralPath $logDir)) {
        New-Item -ItemType Directory -Path $logDir -Force | Out-Null
    }

    $buildGate = New-GateResult -Name "build-ui"
    if (-not $SkipBuild) {
        $buildLog = Join-Path $logDir "repo-skill-build.log"
        $buildGate.logPath = [System.IO.Path]::GetRelativePath($root, $buildLog)
        $buildGate.command = "dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj /p:UseAppHost=false"
        Invoke-GateCommand -Result $buildGate -Action {
            & dotnet build "src/FreeformHelper.UI/FreeformHelper.UI.csproj" "/p:UseAppHost=false" *> $buildLog
            if ($LASTEXITCODE -ne 0) {
                throw "[repo_scan] build failed with exit code $LASTEXITCODE."
            }
        }
    }

    $lintGate = New-GateResult -Name "lint-all-files"
    $lintSummary = @()
    if (-not $SkipLint -and (Test-Path "scripts/tests/lint.ps1")) {
        $lintLog = Join-Path $logDir "repo-skill-lint.log"
        $lintResultPath = Join-Path $logDir "repo-skill-lint-result.json"
        $lintGate.logPath = [System.IO.Path]::GetRelativePath($root, $lintLog)
        $lintGate.command = "./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost -NoThrow -ResultOutputPath `"$lintResultPath`""
        Invoke-GateCommand -Result $lintGate -Action {
            & ./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost -NoThrow -ResultOutputPath $lintResultPath *> $lintLog
            if (Test-Path -LiteralPath $lintResultPath) {
                $lintResult = Get-Content -LiteralPath $lintResultPath -Raw | ConvertFrom-Json
                if ($lintResult.overallStatus -eq "failed") {
                    $lintGate.message = $lintResult.failureMessage
                    $lintGate.failureKind = $lintResult.failureKind
                    throw "[repo_scan] lint reported failure."
                }
            }
        }

        if (Test-Path -LiteralPath $lintLog) {
            $warningCodes = @(
                Select-String -Path $lintLog -Pattern 'warning\s+([A-Z]{2}\d+):' |
                    ForEach-Object { $_.Matches[0].Groups[1].Value } |
                    Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
            )
            if ($warningCodes.Count -gt 0) {
                $lintSummary = $warningCodes | Group-Object | Sort-Object Count -Descending |
                    ForEach-Object {
                        [PSCustomObject]@{
                            Code  = $_.Name
                            Count = $_.Count
                        }
                    }
            }
        }
    }

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("# Repo Refactor Scan")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
    [void]$sb.AppendLine("Branch: $(git branch --show-current)")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("## File counts")
    [void]$sb.AppendLine("- tracked files: $($allTracked.Count)")
    [void]$sb.AppendLine("- src: $srcCount")
    [void]$sb.AppendLine("- tests: $testsCount")
    [void]$sb.AppendLine("- docs: $docsCount")
    [void]$sb.AppendLine("- scripts: $scriptsCount")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("## Line count hotspots")
    foreach ($item in $hotspots) {
        [void]$sb.AppendLine("- $($item.Path): $($item.Lines)")
    }

    [void]$sb.AppendLine()
    [void]$sb.AppendLine("## Direct state mutation hotspots")
    [void]$sb.AppendLine("- scan scope: public mutable collection properties in src/**/*.cs")
    [void]$sb.AppendLine("- hotspot count: $($stateMutationHotspots.Count)")
    if ($stateMutationCategorySummary.Count -gt 0) {
        [void]$sb.AppendLine("- category summary:")
        foreach ($group in $stateMutationCategorySummary) {
            [void]$sb.AppendLine("  - $($group.Name): $($group.Count)")
        }
    }
    if ($stateMutationTop.Count -eq 0) {
        [void]$sb.AppendLine("- no hotspots found by current heuristic.")
    }
    else {
        [void]$sb.AppendLine()
        [void]$sb.AppendLine("### Top hotspots (with convergence route)")
        foreach ($item in $stateMutationTop) {
            [void]$sb.AppendLine("- $($item.Path):$($item.Line) [$($item.Category)]")
            [void]$sb.AppendLine("  - snippet: " + '`' + $item.Snippet + '`')
            [void]$sb.AppendLine("  - convergence route: $($item.ConvergeRoute)")
        }
    }

    [void]$sb.AppendLine()
    [void]$sb.AppendLine("## Build gate")
    [void]$sb.AppendLine("- status: $($buildGate.status)")
    if (-not [string]::IsNullOrWhiteSpace($buildGate.failureKind)) {
        [void]$sb.AppendLine("- failure kind: $($buildGate.failureKind)")
    }
    if (-not [string]::IsNullOrWhiteSpace($buildGate.message)) {
        [void]$sb.AppendLine("- message: $($buildGate.message)")
    }
    if (-not [string]::IsNullOrWhiteSpace($buildGate.logPath)) {
        [void]$sb.AppendLine("- log: $($buildGate.logPath)")
    }

    [void]$sb.AppendLine()
    [void]$sb.AppendLine("## Lint gate")
    [void]$sb.AppendLine("- status: $($lintGate.status)")
    if (-not [string]::IsNullOrWhiteSpace($lintGate.failureKind)) {
        [void]$sb.AppendLine("- failure kind: $($lintGate.failureKind)")
    }
    if (-not [string]::IsNullOrWhiteSpace($lintGate.message)) {
        [void]$sb.AppendLine("- message: $($lintGate.message)")
    }
    if (-not [string]::IsNullOrWhiteSpace($lintGate.logPath)) {
        [void]$sb.AppendLine("- log: $($lintGate.logPath)")
    }

    if ($lintSummary.Count -gt 0) {
        [void]$sb.AppendLine()
        [void]$sb.AppendLine("## Lint warnings (`lint.ps1 -AllFiles`)")
        foreach ($warn in $lintSummary) {
            [void]$sb.AppendLine("- $($warn.Code): $($warn.Count)")
        }
    }

    $output = $sb.ToString().TrimEnd()
    if (-not [string]::IsNullOrWhiteSpace($OutFile)) {
        $dir = Split-Path -Parent $OutFile
        if (-not [string]::IsNullOrWhiteSpace($dir)) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
        }

        Set-Content -Path $OutFile -Value $output
        Write-Output "Wrote: $OutFile"
    }

    Write-Output $output

    $hasCodeGateFailure =
        (($buildGate.status -eq "failed") -and ($buildGate.failureKind -eq "code")) -or
        (($lintGate.status -eq "failed") -and ($lintGate.failureKind -eq "code"))
    if ($hasCodeGateFailure) {
        throw "Repo scan detected build/lint code failures. See report for details."
    }
}
finally {
    Pop-Location
}
