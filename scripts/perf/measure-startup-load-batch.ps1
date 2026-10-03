param(
    [int]$Runs = 5,
    [string]$ProjectPath = "",
    [string]$UiProject = "src/FreeformHelper.UI/FreeformHelper.UI.csproj",
    [string]$ExePath = "build/bin/FreeformHelper.UI/Debug/net8.0/FreeformHelper.UI.exe",
    [int]$StartupTimeoutMs = 60000,
    [int]$LoadTimeoutMs = 120000,
    [int]$PollIntervalMs = 400,
    [switch]$SkipBuild,
    [string]$OutPath = "build/perf/startup-load-batch-latest.md",
    [string]$OutJsonPath = "build/perf/startup-load-batch-latest.json",
    [string]$CompareJsonPath = "",
    [int]$RegressionThresholdPercent = 15,
    [switch]$FailOnRegression
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-OptionalPropertyValue {
    param(
        [Parameter(Mandatory = $false)]
        [object]$Object,
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $false)]
        [object]$DefaultValue = $null
    )

    if ($null -eq $Object) {
        return $DefaultValue
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $DefaultValue
    }

    return $property.Value
}

function Convert-ToNullableDouble {
    param(
        [Parameter(Mandatory = $false)]
        [object]$Value
    )

    if ($null -eq $Value) {
        return $null
    }

    $parsed = 0.0
    if ([double]::TryParse($Value.ToString(), [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$parsed)) {
        return [double]$parsed
    }

    return $null
}

function Get-PercentileValue {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Values,
        [double]$Percentile
    )

    $list = New-Object System.Collections.Generic.List[double]
    foreach ($value in @($Values)) {
        if ($null -eq $value) {
            continue
        }

        [void]$list.Add([double]$value)
    }

    if ($list.Count -eq 0) {
        return $null
    }

    $sorted = @($list.ToArray() | Sort-Object)
    $rank = [Math]::Ceiling(($Percentile / 100.0) * $sorted.Count) - 1
    if ($rank -lt 0) {
        $rank = 0
    }
    if ($rank -ge $sorted.Count) {
        $rank = $sorted.Count - 1
    }

    return [double]$sorted[$rank]
}

function New-Stats {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Values
    )

    $list = New-Object System.Collections.Generic.List[double]
    foreach ($value in @($Values)) {
        if ($null -eq $value) {
            continue
        }

        [void]$list.Add([double]$value)
    }

    if ($list.Count -eq 0) {
        return $null
    }

    $valueArray = $list.ToArray()

    return [PSCustomObject]@{
        count = $valueArray.Length
        min = [int]([Math]::Round(($valueArray | Measure-Object -Minimum).Minimum))
        max = [int]([Math]::Round(($valueArray | Measure-Object -Maximum).Maximum))
        avg = [int]([Math]::Round(($valueArray | Measure-Object -Average).Average))
        p50 = [int]([Math]::Round((Get-PercentileValue -Values $valueArray -Percentile 50)))
        p95 = [int]([Math]::Round((Get-PercentileValue -Values $valueArray -Percentile 95)))
    }
}

if ($Runs -lt 1) {
    throw "-Runs must be >= 1."
}
if ($RegressionThresholdPercent -lt 0) {
    throw "-RegressionThresholdPercent must be >= 0."
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\\..")
$singleRunScript = Join-Path $PSScriptRoot "measure-startup-load.ps1"
if (-not (Test-Path -LiteralPath $singleRunScript)) {
    throw "Missing script: $singleRunScript"
}

Push-Location $repoRoot
try {
    $runOutputDir = Join-Path $repoRoot "build/perf/runs"
    if (-not (Test-Path -LiteralPath $runOutputDir)) {
        New-Item -Path $runOutputDir -ItemType Directory -Force | Out-Null
    }

    $runSummaries = New-Object System.Collections.Generic.List[object]
    for ($run = 1; $run -le $Runs; $run++) {
        $runMd = Join-Path $runOutputDir ("startup-load-run-{0:D2}.md" -f $run)
        $runJson = Join-Path $runOutputDir ("startup-load-run-{0:D2}.json" -f $run)
        $args = @(
            "-ExecutionPolicy", "Bypass",
            "-File", $singleRunScript,
            "-UiProject", $UiProject,
            "-ExePath", $ExePath,
            "-StartupTimeoutMs", $StartupTimeoutMs.ToString([System.Globalization.CultureInfo]::InvariantCulture),
            "-LoadTimeoutMs", $LoadTimeoutMs.ToString([System.Globalization.CultureInfo]::InvariantCulture),
            "-PollIntervalMs", $PollIntervalMs.ToString([System.Globalization.CultureInfo]::InvariantCulture),
            "-OutPath", $runMd,
            "-OutJsonPath", $runJson
        )

        if (-not [string]::IsNullOrWhiteSpace($ProjectPath)) {
            $args += @("-ProjectPath", $ProjectPath)
        }
        if ($SkipBuild -or $run -gt 1) {
            $args += "-SkipBuild"
        }

        Write-Host ("[Run {0}/{1}] Measuring startup/load..." -f $run, $Runs)
        & powershell @args | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Run $run failed. ExitCode=$LASTEXITCODE"
        }

        $result = Get-Content -LiteralPath $runJson -Raw | ConvertFrom-Json
        $load = $result.loadProject
        $runSummaries.Add([PSCustomObject]@{
                run = $run
                startupReadyMs = [int]$result.startup.readyElapsedMs
                loadTotalMs = if ($null -ne $load) { [int]$load.totalElapsedMs } else { $null }
                cadCount = if ($null -ne $load) { [int]$load.cadCount } else { $null }
                regularCount = if ($null -ne $load) { [int]$load.regularCount } else { $null }
                statusText = if ($null -ne $load) { [string]$load.statusText } else { [string]$result.finalStatus.statusText }
            })
    }

    $startupValues = @($runSummaries | ForEach-Object { [double]$_.startupReadyMs })
    $startupStats = New-Stats -Values $startupValues
    $loadValues = @($runSummaries | Where-Object { $null -ne $_.loadTotalMs } | ForEach-Object { [double]$_.loadTotalMs })
    $loadStats = if ($loadValues.Length -gt 0) { New-Stats -Values $loadValues } else { $null }

    $warmRuns = $runSummaries | Where-Object { $_.run -gt 1 }
    $warmStartupValues = @($warmRuns | ForEach-Object { [double]$_.startupReadyMs })
    $warmLoadValues = @($warmRuns | Where-Object { $null -ne $_.loadTotalMs } | ForEach-Object { [double]$_.loadTotalMs })
    $warmStartupStats = if ($warmStartupValues.Length -gt 0) { New-Stats -Values $warmStartupValues } else { $null }
    $warmLoadStats = if ($warmLoadValues.Length -gt 0) { New-Stats -Values $warmLoadValues } else { $null }

    $summary = [PSCustomObject]@{
        generatedUtc = [DateTime]::UtcNow.ToString("o")
        runs = $runSummaries
        startup = [PSCustomObject]@{
            coldMs = [int]$runSummaries[0].startupReadyMs
            all = $startupStats
            warm = $warmStartupStats
        }
        loadProject = [PSCustomObject]@{
            enabled = -not [string]::IsNullOrWhiteSpace($ProjectPath)
            coldMs = if ($null -ne $runSummaries[0].loadTotalMs) { [int]$runSummaries[0].loadTotalMs } else { $null }
            all = $loadStats
            warm = $warmLoadStats
        }
    }

    $regression = [PSCustomObject]@{
        compared = $false
        comparePath = $null
        thresholdPercent = $RegressionThresholdPercent
        checkedCount = 0
        regressionCount = 0
        hasRegression = $false
        items = @()
    }

    if (-not [string]::IsNullOrWhiteSpace($CompareJsonPath)) {
        $resolvedComparePath = if ([System.IO.Path]::IsPathRooted($CompareJsonPath)) {
            [System.IO.Path]::GetFullPath($CompareJsonPath)
        }
        else {
            [System.IO.Path]::GetFullPath((Join-Path $repoRoot $CompareJsonPath))
        }

        if (-not (Test-Path -LiteralPath $resolvedComparePath)) {
            throw "Compare JSON not found: $resolvedComparePath"
        }

        $baseline = Get-Content -LiteralPath $resolvedComparePath -Raw | ConvertFrom-Json
        $items = New-Object System.Collections.Generic.List[object]

        function Add-RegressionItem {
            param(
                [string]$Metric,
                [object]$CurrentValueRaw,
                [object]$BaselineValueRaw
            )

            $currentValue = Convert-ToNullableDouble -Value $CurrentValueRaw
            $baselineValue = Convert-ToNullableDouble -Value $BaselineValueRaw
            if ($null -eq $currentValue -or $null -eq $baselineValue -or $baselineValue -le 0) {
                return
            }

            $deltaPercent = (($currentValue - $baselineValue) / $baselineValue) * 100.0
            $isRegression = $deltaPercent -gt $RegressionThresholdPercent
            $items.Add([PSCustomObject]@{
                    metric = $Metric
                    baseline = [int]([Math]::Round($baselineValue))
                    current = [int]([Math]::Round($currentValue))
                    deltaPercent = [Math]::Round($deltaPercent, 2)
                    isRegression = $isRegression
                }) | Out-Null
        }

        Add-RegressionItem `
            -Metric "startup.coldMs" `
            -CurrentValueRaw (Get-OptionalPropertyValue -Object $summary.startup -Name "coldMs") `
            -BaselineValueRaw (Get-OptionalPropertyValue -Object (Get-OptionalPropertyValue -Object $baseline -Name "startup") -Name "coldMs")
        Add-RegressionItem `
            -Metric "startup.warm.p95" `
            -CurrentValueRaw (Get-OptionalPropertyValue -Object (Get-OptionalPropertyValue -Object $summary.startup -Name "warm") -Name "p95") `
            -BaselineValueRaw (Get-OptionalPropertyValue -Object (Get-OptionalPropertyValue -Object (Get-OptionalPropertyValue -Object $baseline -Name "startup") -Name "warm") -Name "p95")
        Add-RegressionItem `
            -Metric "loadProject.coldMs" `
            -CurrentValueRaw (Get-OptionalPropertyValue -Object $summary.loadProject -Name "coldMs") `
            -BaselineValueRaw (Get-OptionalPropertyValue -Object (Get-OptionalPropertyValue -Object $baseline -Name "loadProject") -Name "coldMs")
        Add-RegressionItem `
            -Metric "loadProject.warm.p95" `
            -CurrentValueRaw (Get-OptionalPropertyValue -Object (Get-OptionalPropertyValue -Object $summary.loadProject -Name "warm") -Name "p95") `
            -BaselineValueRaw (Get-OptionalPropertyValue -Object (Get-OptionalPropertyValue -Object (Get-OptionalPropertyValue -Object $baseline -Name "loadProject") -Name "warm") -Name "p95")

        $itemsArray = $items.ToArray()
        $regressionCount = @($itemsArray | Where-Object { $_.isRegression }).Count
        $regression = [PSCustomObject]@{
            compared = $true
            comparePath = $resolvedComparePath
            thresholdPercent = $RegressionThresholdPercent
            checkedCount = $itemsArray.Count
            regressionCount = $regressionCount
            hasRegression = ($regressionCount -gt 0)
            items = $itemsArray
        }
    }

    Add-Member -InputObject $summary -NotePropertyName regression -NotePropertyValue $regression

    $markdown = New-Object System.Collections.Generic.List[string]
    $markdown.Add("# Startup/Load Batch Measurement")
    $markdown.Add(("Generated: {0}" -f ([DateTime]::UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"))))
    $markdown.Add(("Runs: {0}" -f $Runs))
    if (-not [string]::IsNullOrWhiteSpace($ProjectPath)) {
        $markdown.Add(("Project: {0}" -f $ProjectPath))
    }
    else {
        $markdown.Add("Project: (none)")
    }
    $markdown.Add("")
    $markdown.Add("## Runs")
    $markdown.Add("| Run | Startup Ready (ms) | Load Total (ms) | CAD | Regular | Status |")
    $markdown.Add("| --- | ---: | ---: | ---: | ---: | --- |")
    foreach ($run in $runSummaries) {
        $line = "| {0} | {1} | {2} | {3} | {4} | {5} |" -f
            $run.run,
            $run.startupReadyMs,
            $(if ($null -ne $run.loadTotalMs) { $run.loadTotalMs } else { "-" }),
            $(if ($null -ne $run.cadCount) { $run.cadCount } else { "-" }),
            $(if ($null -ne $run.regularCount) { $run.regularCount } else { "-" }),
            ($run.statusText -replace '\|', '/')
        $markdown.Add($line)
    }

    $markdown.Add("")
    $markdown.Add("## Startup Summary")
    $markdown.Add(("- Cold (run1): {0} ms" -f $summary.startup.coldMs))
    $markdown.Add(("- All runs: avg={0}, p50={1}, p95={2}, min={3}, max={4}" -f
            $summary.startup.all.avg,
            $summary.startup.all.p50,
            $summary.startup.all.p95,
            $summary.startup.all.min,
            $summary.startup.all.max))
    if ($null -ne $summary.startup.warm) {
        $markdown.Add(("- Warm (run2+): avg={0}, p50={1}, p95={2}, min={3}, max={4}" -f
                $summary.startup.warm.avg,
                $summary.startup.warm.p50,
                $summary.startup.warm.p95,
                $summary.startup.warm.min,
                $summary.startup.warm.max))
    }

    if ($summary.loadProject.enabled -and $null -ne $summary.loadProject.all) {
        $markdown.Add("")
        $markdown.Add("## Load Project Summary")
        $markdown.Add(("- Cold (run1): {0} ms" -f $summary.loadProject.coldMs))
        $markdown.Add(("- All runs: avg={0}, p50={1}, p95={2}, min={3}, max={4}" -f
                $summary.loadProject.all.avg,
                $summary.loadProject.all.p50,
                $summary.loadProject.all.p95,
                $summary.loadProject.all.min,
                $summary.loadProject.all.max))
        if ($null -ne $summary.loadProject.warm) {
            $markdown.Add(("- Warm (run2+): avg={0}, p50={1}, p95={2}, min={3}, max={4}" -f
                    $summary.loadProject.warm.avg,
                    $summary.loadProject.warm.p50,
                    $summary.loadProject.warm.p95,
                    $summary.loadProject.warm.min,
                    $summary.loadProject.warm.max))
        }
    }

    if ($summary.regression.compared) {
        $markdown.Add("")
        $markdown.Add("## Regression Check")
        $markdown.Add(("- Compare with: {0}" -f $summary.regression.comparePath))
        $markdown.Add(("- Threshold: +{0} %" -f $summary.regression.thresholdPercent))
        $markdown.Add(("- Checked: {0}, Regressions: {1}" -f $summary.regression.checkedCount, $summary.regression.regressionCount))

        if (@($summary.regression.items).Count -gt 0) {
            $markdown.Add("")
            $markdown.Add("| Metric | Baseline (ms) | Current (ms) | Delta % | Result |")
            $markdown.Add("| --- | ---: | ---: | ---: | --- |")
            foreach ($item in $summary.regression.items) {
                $markdown.Add(("| {0} | {1} | {2} | {3}% | {4} |" -f
                        $item.metric,
                        $item.baseline,
                        $item.current,
                        $item.deltaPercent,
                        $(if ($item.isRegression) { "REGRESSION" } else { "OK" })))
            }
        }
        else {
            $markdown.Add("- No comparable metrics were found between current and baseline JSON.")
        }
    }

    $outDir = Split-Path -Parent $OutPath
    if (-not [string]::IsNullOrWhiteSpace($outDir) -and -not (Test-Path -LiteralPath $outDir)) {
        New-Item -Path $outDir -ItemType Directory -Force | Out-Null
    }

    $jsonDir = Split-Path -Parent $OutJsonPath
    if (-not [string]::IsNullOrWhiteSpace($jsonDir) -and -not (Test-Path -LiteralPath $jsonDir)) {
        New-Item -Path $jsonDir -ItemType Directory -Force | Out-Null
    }

    Set-Content -LiteralPath $OutPath -Value $markdown -Encoding utf8
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutJsonPath -Encoding utf8

    Write-Host "Batch startup/load markdown generated: $OutPath"
    Write-Host "Batch startup/load json generated: $OutJsonPath"
    Write-Host "Per-run outputs: $runOutputDir"

    if ($FailOnRegression -and $summary.regression.hasRegression) {
        throw "Startup/load regression detected. See $OutPath"
    }
}
finally {
    Pop-Location
}
