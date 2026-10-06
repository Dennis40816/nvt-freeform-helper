param(
    [string]$ProjectPath = "example/BOE36.35/project_3635.json",
    [int]$CadId = 4767,
    [int]$RegularId = -1,
    [string]$SelectionCadIds = "",
    [int]$SelectionCycles = 6,
    [string]$OutDir = "build/perf/3635-regression-latest",
    [string]$LogPath = "build/logs/app.log",
    [int]$TimeoutMs = 120000,
    [int]$ReadyTimeoutMs = 90000,
    [int]$RetryCount = 16,
    [int]$RetryDelayMs = 350,
    [string]$GoldenV21Path = "example/BOE36.35/notch_export_v21_current.c",
    [string]$GoldenV22Path = "example/BOE36.35/notch_export_v22_current.c",
    [string]$GoldenManifestPath = "example/BOE36.35/notch_export_golden_manifest.json",
    [string]$BudgetPath = "docs/performance/regression-baseline-3635.budget.json",
    [switch]$SkipBuild,
    [switch]$SkipNotchValidation,
    [switch]$SkipGoldenCheck,
    [switch]$LaunchIsolatedUi,
    [switch]$ReverseCExportOrder,
    [switch]$EnforceBudget,
    [switch]$CompactJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$uiProject = "src/FreeformHelper.UI/FreeformHelper.UI.csproj"
$uiExe = Join-Path $repoRoot "build/bin/FreeformHelper.UI/Debug/net10.0/FreeformHelper.UI.exe"
$runtimeCmd = if (Test-Path -LiteralPath $uiExe) { $uiExe } else { "dotnet" }
$runtimePrefix = if (Test-Path -LiteralPath $uiExe) { @() } else { @("run", "--project", $uiProject, "--") }
$managedUiProcess = $null
$managedUiStatus = $null
$isolatedAppSettingsPath = $null
$settingsOverrideName = "FREEFORMHELPER_APP_GENERAL_SETTINGS_PATH"
$previousSettingsOverride = $null
$settingsOverrideWasSet = $false

function Write-JsonResponse {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Response
    )

    if ($CompactJson) {
        return $Response | ConvertTo-Json -Depth 100 -Compress
    }

    return $Response | ConvertTo-Json -Depth 100
}

function Save-JsonFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [psobject]$Data
    )

    $dir = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($dir) -and -not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    $json = if ($CompactJson) {
        $Data | ConvertTo-Json -Depth 100 -Compress
    }
    else {
        $Data | ConvertTo-Json -Depth 100
    }

    Set-Content -LiteralPath $Path -Value $json -Encoding utf8
}

function Resolve-RepoPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

function Get-GoldenComparisonText {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $text = [System.IO.File]::ReadAllText($Path)
    $normalized = $text.Replace("`r`n", "`n", [System.StringComparison]::Ordinal)
    $normalized = $normalized.Replace("`r", "`n", [System.StringComparison]::Ordinal)
    if ($normalized.EndsWith("`n", [System.StringComparison]::Ordinal)) {
        return $normalized.Substring(0, $normalized.Length - 1)
    }

    return $normalized
}

function Assert-SignedFileContract {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Label,
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$ExpectedSha256,
        [long]$ExpectedBytes = -1,
        [int]$ExpectedNodes = -1
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Signed file not found ($Label): $Path"
    }

    $item = Get-Item -LiteralPath $Path
    if ($ExpectedBytes -ge 0 -and $item.Length -ne $ExpectedBytes) {
        throw "Signed byte count mismatch ($Label). expected=$ExpectedBytes actual=$($item.Length) path=$Path"
    }

    $actualSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
    if (-not [string]::Equals($ExpectedSha256, $actualSha256, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Signed SHA-256 mismatch ($Label). expected=$ExpectedSha256 actual=$actualSha256 path=$Path"
    }

    $actualNodes = $null
    if ($ExpectedNodes -ge 0) {
        $text = [System.IO.File]::ReadAllText($Path)
        $nodeMatch = [System.Text.RegularExpressions.Regex]::Match(
            $text,
            '#define\s+USER_NHC_NODE_NUM\s+\((?<nodes>\d+)u\)',
            [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
        if (-not $nodeMatch.Success) {
            throw "Signed node count macro not found ($Label): $Path"
        }

        $actualNodes = [int]$nodeMatch.Groups["nodes"].Value
        if ($actualNodes -ne $ExpectedNodes) {
            throw "Signed node count mismatch ($Label). expected=$ExpectedNodes actual=$actualNodes path=$Path"
        }
    }

    return [PSCustomObject]@{
        label = $Label
        path = $Path
        bytes = [long]$item.Length
        sha256 = $actualSha256
        nodes = $actualNodes
    }
}

function Assert-GoldenExport {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Format,
        [Parameter(Mandatory = $true)]
        [string]$ActualPath,
        [Parameter(Mandatory = $true)]
        [string]$GoldenPath
    )

    if (-not (Test-Path -LiteralPath $ActualPath)) {
        throw "Golden export actual file not found: $ActualPath"
    }

    if (-not (Test-Path -LiteralPath $GoldenPath)) {
        throw "Golden export reference file not found: $GoldenPath"
    }

    $expected = Get-GoldenComparisonText -Path $GoldenPath
    $actual = Get-GoldenComparisonText -Path $ActualPath
    if (-not [string]::Equals($expected, $actual, [System.StringComparison]::Ordinal)) {
        $limit = [System.Math]::Min($expected.Length, $actual.Length)
        $index = 0
        while ($index -lt $limit -and [int]$expected[$index] -eq [int]$actual[$index]) {
            $index++
        }

        $reference = if ($index -lt $expected.Length) { $expected } else { $actual }
        $prefix = if ($index -eq 0) { [string]::Empty } else { $reference.Substring(0, $index) }
        $line = [System.Text.RegularExpressions.Regex]::Matches($prefix, "`n").Count + 1
        $column = $index - $prefix.LastIndexOf("`n", [System.StringComparison]::Ordinal)
        $expectedValue = if ($index -ge $expected.Length) { "<EOF>" } else { "U+{0:X4}" -f [int]$expected[$index] }
        $actualValue = if ($index -ge $actual.Length) { "<EOF>" } else { "U+{0:X4}" -f [int]$actual[$index] }
        throw ("Golden export mismatch ({0}). actual={1} golden={2} firstDiff=line {3}, column {4}, expected={5}, actual={6}" -f
            $Format,
            $ActualPath,
            $GoldenPath,
            $line,
            $column,
            $expectedValue,
            $actualValue)
    }

    return [PSCustomObject]@{
        format = $Format
        skipped = $false
        pass = $true
        goldenPath = $GoldenPath
        actualPath = $ActualPath
    }
}

function Convert-ToCmdToken {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    if ($Value.IndexOfAny(@([char]' ', [char]'"', [char]'&', [char]'|', [char]'(', [char]')', [char]'[', [char]']')) -ge 0) {
        return '"' + ($Value -replace '"', '\"') + '"'
    }

    return $Value
}

function Invoke-RuntimeQueryInternal {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$QueryArgs
    )

    $cmd = $runtimePrefix + @(
        "query"
    ) + $QueryArgs + @(
        "--timeout-ms", $TimeoutMs.ToString([System.Globalization.CultureInfo]::InvariantCulture),
        "--json-compact"
    )

    $cmdTokens = @($runtimeCmd) + $cmd
    $cmdLine = ($cmdTokens | ForEach-Object { Convert-ToCmdToken -Value $_ }) -join " "

    Write-Host (">> " + $cmdLine) -ForegroundColor DarkGray
    $stdoutPath = [System.IO.Path]::GetTempFileName()
    $stderrPath = [System.IO.Path]::GetTempFileName()
    $cmdLineWithRedirect = $cmdLine + " 1>`"" + $stdoutPath + "`" 2>`"" + $stderrPath + "`""
    try {
        $global:LASTEXITCODE = 0
        cmd /d /c $cmdLineWithRedirect | Out-Null
        $exitCode = $global:LASTEXITCODE
        $stdoutText = if (Test-Path -LiteralPath $stdoutPath) { (Get-Content -LiteralPath $stdoutPath -Raw) } else { [string]::Empty }
        $stderrText = if (Test-Path -LiteralPath $stderrPath) { (Get-Content -LiteralPath $stderrPath -Raw) } else { [string]::Empty }
    }
    finally {
        if (Test-Path -LiteralPath $stdoutPath) {
            Remove-Item -LiteralPath $stdoutPath -Force -ErrorAction SilentlyContinue
        }

        if (Test-Path -LiteralPath $stderrPath) {
            Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
        }
    }

    if ($exitCode -ne 0) {
        throw "Runtime query process failed. exitCode=$exitCode args=$($QueryArgs -join ' ') stderr=$stderrText"
    }

    $text = [System.Text.RegularExpressions.Regex]::Replace(
        $stdoutText,
        "\x1B\[[0-9;?]*[ -/]*[@-~]",
        [string]::Empty)
    $text = [System.Text.RegularExpressions.Regex]::Replace(
        $text,
        "[\x00-\x1F]",
        [string]::Empty).Trim()
    if ([string]::IsNullOrWhiteSpace($text)) {
        throw "Runtime query returned empty output. args=$($QueryArgs -join ' ') stderr=$stderrText"
    }

    try {
        return $text | ConvertFrom-Json
    }
    catch {
        $firstBrace = $text.IndexOf("{", [System.StringComparison]::Ordinal)
        $lastBrace = $text.LastIndexOf("}", [System.StringComparison]::Ordinal)
        if ($firstBrace -ge 0 -and $lastBrace -gt $firstBrace) {
            $candidate = $text.Substring($firstBrace, $lastBrace - $firstBrace + 1)
            try {
                return $candidate | ConvertFrom-Json
            }
            catch {
                throw "Runtime query returned invalid JSON. args=$($QueryArgs -join ' ') parseError=$($_.Exception.Message)`nRaw:`n$text"
            }
        }

        throw "Runtime query returned invalid JSON. args=$($QueryArgs -join ' ') parseError=$($_.Exception.Message)`nRaw:`n$text"
    }
}

function Invoke-RuntimeQuery {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$QueryArgs,
        [string[]]$RetryErrorCodes = @()
    )

    for ($attempt = 0; $attempt -le $RetryCount; $attempt++) {
        try {
            $response = Invoke-RuntimeQueryInternal -QueryArgs $QueryArgs
        }
        catch {
            $message = $_.Exception.Message
            $isTransientProcessError =
                $message -like "Runtime query returned empty output.*" -or
                $message -like "Runtime query returned invalid JSON.*" -or
                $message -like "Runtime query process failed.*"
            if ($attempt -lt $RetryCount -and $isTransientProcessError) {
                Write-Warning ("Transient runtime process error (attempt {0}/{1}): {2}" -f ($attempt + 1), ($RetryCount + 1), $message)
                Start-Sleep -Milliseconds $RetryDelayMs
                continue
            }

            throw
        }

        if ($response.ok) {
            Write-Host (Write-JsonResponse -Response $response)
            return $response
        }

        $code = [string]$response.error.code
        $message = [string]$response.error.message
        $canRetry = $attempt -lt $RetryCount -and $RetryErrorCodes -contains $code
        if (-not $canRetry) {
            Write-Host (Write-JsonResponse -Response $response)
            throw "Runtime query failed. code=$code message=$message args=$($QueryArgs -join ' ')"
        }

        Write-Warning ("Transient runtime error (attempt {0}/{1}): {2} - {3}" -f ($attempt + 1), ($RetryCount + 1), $code, $message)
        Start-Sleep -Milliseconds $RetryDelayMs
    }

    throw "Runtime query retry exhausted. args=$($QueryArgs -join ' ')"
}

function Wait-Workflow {
    param(
        [Parameter(Mandatory = $true)]
        [ScriptBlock]$Predicate,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $ReadyTimeoutMs) {
        $status = Invoke-RuntimeQuery -QueryArgs @("status") -RetryErrorCodes @("INSTANCE_NOT_RUNNING", "IPC_IO_ERROR", "IPC_ERROR")
        if ($status.ok -and (& $Predicate $status.data)) {
            return $status
        }

        Start-Sleep -Milliseconds $RetryDelayMs
    }

    throw "Wait timeout: $Description (>${ReadyTimeoutMs}ms)."
}

function Parse-IdList {
    param(
        [string]$Raw
    )

    if ([string]::IsNullOrWhiteSpace($Raw)) {
        return @()
    }

    $items = New-Object System.Collections.Generic.List[int]
    foreach ($token in $Raw.Split(",", [System.StringSplitOptions]::RemoveEmptyEntries)) {
        $trimmed = $token.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) {
            continue
        }

        $parsed = 0
        if (-not [int]::TryParse($trimmed, [ref]$parsed)) {
            throw "Invalid id token '$trimmed'."
        }

        if ($parsed -lt 0) {
            throw "ID must be >= 0. token='$trimmed'"
        }

        if (-not $items.Contains($parsed)) {
            $items.Add($parsed)
        }
    }

    return $items.ToArray()
}

function Get-Percentile {
    param(
        [int[]]$SortedValues,
        [double]$Percentile
    )

    if ($null -eq $SortedValues -or $SortedValues.Count -eq 0) {
        return 0
    }

    if ($SortedValues.Count -eq 1) {
        return $SortedValues[0]
    }

    $rank = [Math]::Ceiling($Percentile * $SortedValues.Count) - 1
    $index = [Math]::Min([Math]::Max([int]$rank, 0), $SortedValues.Count - 1)
    return $SortedValues[$index]
}

function Build-Stat {
    param(
        [int[]]$Values
    )

    if ($null -eq $Values -or $Values.Count -eq 0) {
        return [PSCustomObject]@{
            count = 0
            min = 0
            p50 = 0
            p95 = 0
            max = 0
            avg = 0.0
        }
    }

    $sorted = @($Values | Sort-Object)
    $avg = [Math]::Round((($sorted | Measure-Object -Average).Average), 2)
    return [PSCustomObject]@{
        count = $sorted.Count
        min = $sorted[0]
        p50 = Get-Percentile -SortedValues $sorted -Percentile 0.50
        p95 = Get-Percentile -SortedValues $sorted -Percentile 0.95
        max = $sorted[$sorted.Count - 1]
        avg = $avg
    }
}

function Load-BudgetConfig {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $resolvedPath = if ([System.IO.Path]::IsPathRooted($Path)) {
        $Path
    }
    else {
        Join-Path $repoRoot $Path
    }

    if (-not (Test-Path -LiteralPath $resolvedPath)) {
        throw "Budget config not found: $resolvedPath"
    }

    return Get-Content -LiteralPath $resolvedPath -Raw | ConvertFrom-Json
}

function New-BudgetCheck {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [long]$Actual,
        [Parameter(Mandatory = $true)]
        [long]$Budget,
        [string]$Unit = "ms"
    )

    return [PSCustomObject]@{
        name = $Name
        actual = $Actual
        budget = $Budget
        unit = $Unit
        pass = ($Actual -le $Budget)
    }
}

function Build-BudgetGate {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Budget,
        [Parameter(Mandatory = $true)]
        [psobject]$SelectionStats,
        [Parameter(Mandatory = $true)]
        [psobject[]]$ExportArtifacts
    )

    $checks = New-Object System.Collections.Generic.List[object]
    $checks.Add([PSCustomObject]@{
        name = "selection.sampleCount"
        actual = [int]$SelectionStats.sampleCount
        budget = [int]$Budget.selectionSampleCountMin
        unit = "count"
        pass = ([int]$SelectionStats.sampleCount -ge [int]$Budget.selectionSampleCountMin)
    }) | Out-Null
    $checks.Add((New-BudgetCheck -Name "selection.total.p95" -Actual ([long]$SelectionStats.totalMs.p95) -Budget ([long]$Budget.selectionTotalP95Ms))) | Out-Null
    $checks.Add((New-BudgetCheck -Name "selection.inspector.p95" -Actual ([long]$SelectionStats.inspectorMs.p95) -Budget ([long]$Budget.selectionInspectorP95Ms))) | Out-Null

    $csvArtifact = $ExportArtifacts | Where-Object { $_.format -eq "csv" } | Select-Object -First 1
    if ($null -ne $csvArtifact) {
        $checks.Add((New-BudgetCheck -Name "export.csv.elapsed" -Actual ([long]$csvArtifact.elapsedMs) -Budget ([long]$Budget.exportCsvElapsedMs))) | Out-Null
    }

    $cv22Artifact = $ExportArtifacts | Where-Object { $_.format -eq "c-v22" } | Select-Object -First 1
    if ($null -ne $cv22Artifact) {
        $checks.Add((New-BudgetCheck -Name "export.c-v22.elapsed" -Actual ([long]$cv22Artifact.elapsedMs) -Budget ([long]$Budget.exportCv22ElapsedMs))) | Out-Null
    }

    $failed = @($checks | Where-Object { -not $_.pass })
    return [PSCustomObject]@{
        pass = ($failed.Count -eq 0)
        checks = $checks
        failed = $failed
    }
}

function Parse-SelectionLatency {
    param(
        [string[]]$Lines
    )

    $regex = [regex]'Selection updated:\s*CAD=(?<cad>\d+),\s*Regular=(?<regular>\d+),\s*summary=(?<summary>\d+)ms,\s*inspector=(?<inspector>\d+)ms,\s*notchPreview=(?<notch>\d+)ms,\s*total=(?<total>\d+)ms(?:\..*)?$'
    $samples = New-Object System.Collections.Generic.List[object]
    foreach ($line in $Lines) {
        $match = $regex.Match($line)
        if (-not $match.Success) {
            continue
        }

        $samples.Add([PSCustomObject]@{
            cadCount = [int]$match.Groups["cad"].Value
            regularCount = [int]$match.Groups["regular"].Value
            summaryMs = [int]$match.Groups["summary"].Value
            inspectorMs = [int]$match.Groups["inspector"].Value
            notchPreviewMs = [int]$match.Groups["notch"].Value
            totalMs = [int]$match.Groups["total"].Value
        })
    }

    return $samples
}

Push-Location $repoRoot
try {
    if ($LaunchIsolatedUi) {
        $existingUiProcesses = @(
            Get-CimInstance Win32_Process |
                Where-Object {
                    $commandLine = [string]$_.CommandLine
                    $isRuntimeQueryClient = $commandLine -match '(?i)(^|\s)query(\s|$)'
                    -not $isRuntimeQueryClient -and (
                        $_.Name -eq "FreeformHelper.UI.exe" -or
                        ($_.Name -eq "dotnet.exe" -and $commandLine -like "*FreeformHelper.UI*")
                    )
                }
        )
        if ($existingUiProcesses.Count -gt 0) {
            $processList = $existingUiProcesses |
                ForEach-Object { "PID=$($_.ProcessId), Name=$($_.Name)" }
            throw "Close every existing FreeformHelper.UI instance before using -LaunchIsolatedUi. Found: $($processList -join '; ')"
        }
    }

    if (-not $SkipBuild) {
        Write-Host "Building UI project..." -ForegroundColor Cyan
        dotnet build $uiProject --nologo /p:UseAppHost=true
        if ($LASTEXITCODE -ne 0) {
            throw "UI build failed with exit code $LASTEXITCODE."
        }
    }

    if ($SelectionCycles -lt 1) {
        throw "SelectionCycles must be >= 1."
    }

    $resolvedOutDir = Join-Path $repoRoot $OutDir
    if (-not (Test-Path -LiteralPath $resolvedOutDir)) {
        New-Item -Path $resolvedOutDir -ItemType Directory -Force | Out-Null
    }

    $csvPath = Join-Path $resolvedOutDir "notch_table.csv"
    $cv21Path = Join-Path $resolvedOutDir "notch_v2.1.c"
    $cv22Path = Join-Path $resolvedOutDir "notch_v2.2.c"
    $resolvedGoldenV21Path = Resolve-RepoPath -Path $GoldenV21Path
    $resolvedGoldenV22Path = Resolve-RepoPath -Path $GoldenV22Path
    $resolvedGoldenManifestPath = Resolve-RepoPath -Path $GoldenManifestPath

    foreach ($actualPath in @($csvPath, $cv21Path, $cv22Path)) {
        foreach ($goldenPath in @($resolvedGoldenV21Path, $resolvedGoldenV22Path, $resolvedGoldenManifestPath)) {
            if ([string]::Equals(
                    [System.IO.Path]::GetFullPath($actualPath),
                    [System.IO.Path]::GetFullPath($goldenPath),
                    [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Output path must not overwrite a signed golden input. output=$actualPath golden=$goldenPath"
            }
        }
    }

    $signedInputs = @()
    if (-not $SkipGoldenCheck) {
        if (-not (Test-Path -LiteralPath $resolvedGoldenManifestPath)) {
            throw "Golden manifest not found: $resolvedGoldenManifestPath"
        }

        $goldenManifest = Get-Content -LiteralPath $resolvedGoldenManifestPath -Raw | ConvertFrom-Json
        $signedInputs = @(
            Assert-SignedFileContract `
                -Label "Lucid 3635 project" `
                -Path (Resolve-RepoPath -Path $ProjectPath) `
                -ExpectedSha256 ([string]$goldenManifest.project.sha256)
            Assert-SignedFileContract `
                -Label "Lucid 3635 regular mask" `
                -Path (Resolve-RepoPath -Path ([string]$goldenManifest.regularMask.path)) `
                -ExpectedSha256 ([string]$goldenManifest.regularMask.sha256)
            Assert-SignedFileContract `
                -Label "Lucid 3635 V2.1 golden" `
                -Path $resolvedGoldenV21Path `
                -ExpectedSha256 ([string]$goldenManifest.outputs.v21.sha256) `
                -ExpectedBytes ([long]$goldenManifest.outputs.v21.bytes) `
                -ExpectedNodes ([int]$goldenManifest.outputs.v21.nodes)
            Assert-SignedFileContract `
                -Label "Lucid 3635 V2.2 golden" `
                -Path $resolvedGoldenV22Path `
                -ExpectedSha256 ([string]$goldenManifest.outputs.v22.sha256) `
                -ExpectedBytes ([long]$goldenManifest.outputs.v22.bytes) `
                -ExpectedNodes ([int]$goldenManifest.outputs.v22.nodes)
        )
    }

    if (-not $SkipGoldenCheck -and -not $LaunchIsolatedUi) {
        throw "Exact golden gate requires -LaunchIsolatedUi so personal app settings cannot change DXF import or export inputs."
    }

    if ($LaunchIsolatedUi) {
        if (-not (Test-Path -LiteralPath $uiExe)) {
            throw "Managed UI executable not found: $uiExe. Re-run without -SkipBuild, or build with /p:UseAppHost=true first."
        }

        $runtimeCmd = $uiExe
        $runtimePrefix = @()
        $isolatedAppSettingsPath = Join-Path $repoRoot (
            "build/tmp/r13.002-app-settings-{0}.json" -f [System.Guid]::NewGuid().ToString("N"))
        $isolatedSettingsDirectory = Split-Path -Parent $isolatedAppSettingsPath
        if (-not (Test-Path -LiteralPath $isolatedSettingsDirectory)) {
            New-Item -Path $isolatedSettingsDirectory -ItemType Directory -Force | Out-Null
        }

        $previousSettingsOverride = [System.Environment]::GetEnvironmentVariable(
            $settingsOverrideName,
            [System.EnvironmentVariableTarget]::Process)
        [System.Environment]::SetEnvironmentVariable(
            $settingsOverrideName,
            $isolatedAppSettingsPath,
            [System.EnvironmentVariableTarget]::Process)
        $settingsOverrideWasSet = $true
        $managedUiProcess = Start-Process `
            -FilePath $uiExe `
            -WorkingDirectory $repoRoot `
            -WindowStyle Hidden `
            -PassThru

        Write-Host ("Started isolated UI instance (PID {0})." -f $managedUiProcess.Id) -ForegroundColor Cyan
        $managedUiStatus = Invoke-RuntimeQuery `
            -QueryArgs @("status") `
            -RetryErrorCodes @("INSTANCE_NOT_RUNNING", "IPC_IO_ERROR", "IPC_ERROR")
        $serverProcessId = [int]$managedUiStatus.data.processId
        if ($serverProcessId -ne $managedUiProcess.Id) {
            throw "Runtime IPC identity mismatch. managedPid=$($managedUiProcess.Id) serverPid=$serverProcessId"
        }
    }

    $resolvedLogPath = Join-Path $repoRoot $LogPath
    $logDirectory = Split-Path -Parent $resolvedLogPath
    if (-not [string]::IsNullOrWhiteSpace($logDirectory) -and -not (Test-Path -LiteralPath $logDirectory)) {
        New-Item -Path $logDirectory -ItemType Directory -Force | Out-Null
    }
    if (-not (Test-Path -LiteralPath $resolvedLogPath)) {
        Set-Content -LiteralPath $resolvedLogPath -Value @() -Encoding utf8
    }

    $budget = Load-BudgetConfig -Path $BudgetPath
    $responses = [ordered]@{}
    if ($null -ne $managedUiStatus) {
        $responses.uiInstanceStatus = $managedUiStatus
    }
    $artifacts = [ordered]@{}

    Write-Host "Loading project and executing workflow Step 1~4..." -ForegroundColor Cyan
    $responses.loadProject = Invoke-RuntimeQuery -QueryArgs @("load-project", "--path", $ProjectPath) -RetryErrorCodes @("INSTANCE_NOT_RUNNING", "IPC_IO_ERROR", "IPC_ERROR")
    Wait-Workflow -Description "workflow has CAD+grid after load-project" -Predicate {
        param($statusData)
        return [bool]$statusData.workflow.hasCad -and [bool]$statusData.workflow.hasGrid
    } | Out-Null

    $responses.runStep1 = Invoke-RuntimeQuery -QueryArgs @("run-step", "--step", "1") -RetryErrorCodes @("STEP_NOT_READY", "NOT_READY")
    Wait-Workflow -Description "step1 ready" -Predicate {
        param($statusData)
        return [bool]$statusData.workflow.hasStep1Result
    } | Out-Null

    $responses.runStep2 = Invoke-RuntimeQuery -QueryArgs @("run-step", "--step", "2") -RetryErrorCodes @("STEP_NOT_READY", "NOT_READY")
    Wait-Workflow -Description "step2 ready" -Predicate {
        param($statusData)
        return [bool]$statusData.workflow.hasStep2Result
    } | Out-Null

    $cadIdText = $CadId.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    $responses.selectCad = Invoke-RuntimeQuery -QueryArgs @("select-cad", "--cad-id", $cadIdText) -RetryErrorCodes @("STEP_NOT_READY", "NOT_READY")
    $responses.runStep3 = Invoke-RuntimeQuery -QueryArgs @("run-step", "--step", "3") -RetryErrorCodes @("STEP_NOT_READY", "NOT_READY")
    Wait-Workflow -Description "step3 ready" -Predicate {
        param($statusData)
        return [bool]$statusData.workflow.hasStep3Result
    } | Out-Null

    $responses.runStep4 = Invoke-RuntimeQuery -QueryArgs @("run-step", "--step", "4") -RetryErrorCodes @("STEP_NOT_READY", "NOT_READY")
    Wait-Workflow -Description "step4 ready" -Predicate {
        param($statusData)
        return [bool]$statusData.workflow.hasStep4Result
    } | Out-Null
    $responses.statusBeforeExports = Invoke-RuntimeQuery -QueryArgs @("status") -RetryErrorCodes @("INSTANCE_NOT_RUNNING", "IPC_IO_ERROR", "IPC_ERROR")

    Write-Host "Executing Step 5 exports (CSV/C v2.1/C v2.2)..." -ForegroundColor Cyan
    $responses.exportCsv = Invoke-RuntimeQuery -QueryArgs @("export-notch", "--format", "csv", "--path", $csvPath) -RetryErrorCodes @("STEP_NOT_READY", "NOT_READY")
    $cExportSteps = if ($ReverseCExportOrder) {
        @(
            [PSCustomObject]@{ Format = "c-v22"; Path = $cv22Path; ResponseKey = "exportCv22" }
            [PSCustomObject]@{ Format = "c-v21"; Path = $cv21Path; ResponseKey = "exportCv21" }
        )
    }
    else {
        @(
            [PSCustomObject]@{ Format = "c-v21"; Path = $cv21Path; ResponseKey = "exportCv21" }
            [PSCustomObject]@{ Format = "c-v22"; Path = $cv22Path; ResponseKey = "exportCv22" }
        )
    }
    foreach ($step in $cExportSteps) {
        $responses[$step.ResponseKey] = Invoke-RuntimeQuery `
            -QueryArgs @("export-notch", "--format", $step.Format, "--path", $step.Path) `
            -RetryErrorCodes @("STEP_NOT_READY", "NOT_READY")
    }

    $resolvedGoldenV21Path = Resolve-RepoPath -Path $GoldenV21Path
    $resolvedGoldenV22Path = Resolve-RepoPath -Path $GoldenV22Path
    $goldenChecks = if ($SkipGoldenCheck) {
        @(
            [PSCustomObject]@{ format = "c-v21"; skipped = $true; pass = $null; actualPath = $cv21Path; goldenPath = $resolvedGoldenV21Path }
            [PSCustomObject]@{ format = "c-v22"; skipped = $true; pass = $null; actualPath = $cv22Path; goldenPath = $resolvedGoldenV22Path }
        )
    }
    else {
        Write-Host "Comparing C exports with checked-in golden files..." -ForegroundColor Cyan
        @(
            Assert-GoldenExport `
                -Format "c-v21" `
                -ActualPath $cv21Path `
                -GoldenPath $resolvedGoldenV21Path
            Assert-GoldenExport `
                -Format "c-v22" `
                -ActualPath $cv22Path `
                -GoldenPath $resolvedGoldenV22Path
        )
    }
    $signedActuals = @()
    if (-not $SkipGoldenCheck) {
        $signedActuals = @(
            Assert-SignedFileContract `
                -Label "Lucid 3635 V2.1 actual" `
                -Path $cv21Path `
                -ExpectedSha256 ([string]$goldenManifest.outputs.v21.sha256) `
                -ExpectedBytes ([long]$goldenManifest.outputs.v21.bytes) `
                -ExpectedNodes ([int]$goldenManifest.outputs.v21.nodes)
            Assert-SignedFileContract `
                -Label "Lucid 3635 V2.2 actual" `
                -Path $cv22Path `
                -ExpectedSha256 ([string]$goldenManifest.outputs.v22.sha256) `
                -ExpectedBytes ([long]$goldenManifest.outputs.v22.bytes) `
                -ExpectedNodes ([int]$goldenManifest.outputs.v22.nodes)
        )
    }

    Write-Host "Collecting runtime query baselines..." -ForegroundColor Cyan
    $responses.status = Invoke-RuntimeQuery -QueryArgs @("status") -RetryErrorCodes @("INSTANCE_NOT_RUNNING", "IPC_IO_ERROR", "IPC_ERROR")
    $exportStateBefore = $responses.statusBeforeExports.data.notchExportState
    $exportStateAfter = $responses.status.data.notchExportState
    foreach ($field in @("enableV21", "enableV22", "fileType", "profile")) {
        $beforeValue = [string]$exportStateBefore.$field
        $afterValue = [string]$exportStateAfter.$field
        if (-not [string]::Equals($beforeValue, $afterValue, [System.StringComparison]::Ordinal)) {
            throw "Runtime export state changed across sequential exports. field=$field before=$beforeValue after=$afterValue"
        }
    }
    $responses.selection = Invoke-RuntimeQuery -QueryArgs @("selection") -RetryErrorCodes @("INSTANCE_NOT_RUNNING", "IPC_IO_ERROR", "IPC_ERROR")
    $responses.notchStage = Invoke-RuntimeQuery -QueryArgs @("notch-stage", "--cad-id", $cadIdText, "--polygon-limit", "128") -RetryErrorCodes @("NOT_READY", "STEP_NOT_READY")
    $responses.notch = Invoke-RuntimeQuery -QueryArgs @("notch", "--cad-id", $cadIdText, "--limit", "400", "--target-limit", "128", "--polygon-limit", "128") -RetryErrorCodes @("NOT_READY", "STEP_NOT_READY")

    $resolvedRegularId = $RegularId
    if ($resolvedRegularId -lt 0) {
        $regularCandidates = @($responses.notch.data.regulars)
        if ($regularCandidates.Count -gt 0) {
            $resolvedRegularId = [int]$regularCandidates[0].regularPadId
        }
    }

    if ($SkipNotchValidation) {
        $responses.notchValidation = [PSCustomObject]@{
            ok = $false
            error = [PSCustomObject]@{
                code = "SKIPPED"
                message = "Skipped by -SkipNotchValidation."
            }
        }
    }
    elseif ($resolvedRegularId -ge 0) {
        $regularIdText = $resolvedRegularId.ToString([System.Globalization.CultureInfo]::InvariantCulture)
        $responses.notchValidation = Invoke-RuntimeQuery -QueryArgs @("notch-validation", "--regular-id", $regularIdText) -RetryErrorCodes @("NOT_READY", "STEP_NOT_READY")
    }
    else {
        $responses.notchValidation = [PSCustomObject]@{
            ok = $false
            error = [PSCustomObject]@{
                code = "SKIPPED"
                message = "No regular-id provided and notch query returned no regular candidates."
            }
        }
    }

    Write-Host "Sampling selection latency..." -ForegroundColor Cyan
    $sampleCadIds = @(Parse-IdList -Raw $SelectionCadIds)
    if ($sampleCadIds.Count -eq 0) {
        $sampleCadIds = @($CadId)
    }

    $allLinesBeforeSampling = Get-Content -LiteralPath $resolvedLogPath
    $sampleStartLineCount = $allLinesBeforeSampling.Count
    $selectionSamples = New-Object System.Collections.Generic.List[object]

    for ($cycle = 0; $cycle -lt $SelectionCycles; $cycle++) {
        foreach ($sampleCadId in $sampleCadIds) {
            $sampleCadIdText = $sampleCadId.ToString([System.Globalization.CultureInfo]::InvariantCulture)
            Invoke-RuntimeQuery -QueryArgs @("clear-selection") -RetryErrorCodes @("INSTANCE_NOT_RUNNING", "IPC_IO_ERROR", "IPC_ERROR") | Out-Null
            $selectResponse = Invoke-RuntimeQuery -QueryArgs @("select-cad", "--cad-id", $sampleCadIdText) -RetryErrorCodes @("STEP_NOT_READY", "NOT_READY")
            $selectionPayload = $selectResponse.data.selection
            $timings = $selectionPayload.timings
            if ($null -ne $timings) {
                $selectionSamples.Add([PSCustomObject]@{
                    cadId = $sampleCadId
                    cycle = $cycle + 1
                    cadCount = @($selectionPayload.selectedCadIds).Count
                    regularCount = @($selectionPayload.selectedRegularIndices).Count
                    summaryMs = [int]$timings.summaryMs
                    inspectorMs = [int]$timings.inspectorMs
                    notchPreviewMs = [int]$timings.notchPreviewMs
                    totalMs = [int]$timings.totalMs
                }) | Out-Null
            }
        }
    }
    Invoke-RuntimeQuery -QueryArgs @("clear-selection") -RetryErrorCodes @("INSTANCE_NOT_RUNNING", "IPC_IO_ERROR", "IPC_ERROR") | Out-Null

    if ($selectionSamples.Count -eq 0) {
        $allLinesAfterSampling = Get-Content -LiteralPath $resolvedLogPath
        $sampleLines = @($allLinesAfterSampling | Select-Object -Skip $sampleStartLineCount)
        foreach ($sample in (Parse-SelectionLatency -Lines $sampleLines)) {
            $selectionSamples.Add($sample) | Out-Null
        }
    }

    $selectionTotalValues = @($selectionSamples | Select-Object -ExpandProperty totalMs)
    $selectionInspectorValues = @($selectionSamples | Select-Object -ExpandProperty inspectorMs)
    $selectionNotchValues = @($selectionSamples | Select-Object -ExpandProperty notchPreviewMs)
    $selectionStats = [PSCustomObject]@{
        sampleCadIds = $sampleCadIds
        cycles = $SelectionCycles
        sampleCount = $selectionSamples.Count
        totalMs = Build-Stat -Values $selectionTotalValues
        inspectorMs = Build-Stat -Values $selectionInspectorValues
        notchPreviewMs = Build-Stat -Values $selectionNotchValues
        samples = $selectionSamples
    }

    $artifactRows = @()
    foreach ($artifact in @(
            [PSCustomObject]@{ format = "csv"; path = $csvPath },
            [PSCustomObject]@{ format = "c-v21"; path = $cv21Path },
            [PSCustomObject]@{ format = "c-v22"; path = $cv22Path })) {
        $exists = Test-Path -LiteralPath $artifact.path
        $size = if ($exists) { (Get-Item -LiteralPath $artifact.path).Length } else { 0 }
        $responseKey = switch ($artifact.format) {
            "csv" { "exportCsv" }
            "c-v21" { "exportCv21" }
            "c-v22" { "exportCv22" }
        }
        $elapsedMs = if ($responses[$responseKey].ok -and $null -ne $responses[$responseKey].data.elapsedMs) {
            [long]$responses[$responseKey].data.elapsedMs
        }
        else {
            0L
        }
        $artifactRows += [PSCustomObject]@{
            format = $artifact.format
            path = $artifact.path
            exists = $exists
            size = $size
            elapsedMs = $elapsedMs
        }
    }
    $artifacts.exports = $artifactRows

    $budgetGate = Build-BudgetGate -Budget $budget -SelectionStats $selectionStats -ExportArtifacts $artifactRows

    Save-JsonFile -Path (Join-Path $resolvedOutDir "runtime-load-project.json") -Data $responses.loadProject
    Save-JsonFile -Path (Join-Path $resolvedOutDir "runtime-run-step1.json") -Data $responses.runStep1
    Save-JsonFile -Path (Join-Path $resolvedOutDir "runtime-run-step2.json") -Data $responses.runStep2
    Save-JsonFile -Path (Join-Path $resolvedOutDir "runtime-run-step3.json") -Data $responses.runStep3
    Save-JsonFile -Path (Join-Path $resolvedOutDir "runtime-run-step4.json") -Data $responses.runStep4
    Save-JsonFile -Path (Join-Path $resolvedOutDir "runtime-status-before-exports.json") -Data $responses.statusBeforeExports
    if ($responses.Contains("uiInstanceStatus")) {
        Save-JsonFile -Path (Join-Path $resolvedOutDir "runtime-ui-instance-status.json") -Data $responses.uiInstanceStatus
    }
    Save-JsonFile -Path (Join-Path $resolvedOutDir "runtime-export-csv.json") -Data $responses.exportCsv
    Save-JsonFile -Path (Join-Path $resolvedOutDir "runtime-export-c-v21.json") -Data $responses.exportCv21
    Save-JsonFile -Path (Join-Path $resolvedOutDir "runtime-export-c-v22.json") -Data $responses.exportCv22
    Save-JsonFile -Path (Join-Path $resolvedOutDir "runtime-status.json") -Data $responses.status
    Save-JsonFile -Path (Join-Path $resolvedOutDir "runtime-selection.json") -Data $responses.selection
    Save-JsonFile -Path (Join-Path $resolvedOutDir "runtime-notch-stage.json") -Data $responses.notchStage
    Save-JsonFile -Path (Join-Path $resolvedOutDir "runtime-notch.json") -Data $responses.notch
    Save-JsonFile -Path (Join-Path $resolvedOutDir "runtime-notch-validation.json") -Data $responses.notchValidation
    Save-JsonFile -Path (Join-Path $resolvedOutDir "selection-latency.json") -Data $selectionStats

    $generatedUtc = [DateTime]::UtcNow
    $summary = [PSCustomObject]@{
        generatedUtc = $generatedUtc.ToString("o")
        projectPath = $ProjectPath
        cadId = $CadId
        regularId = $resolvedRegularId
        cExportOrder = @($cExportSteps.Format)
        uiInstance = [PSCustomObject]@{
            mode = if ($LaunchIsolatedUi) { "managed-isolated" } else { "existing" }
            processId = if ($null -ne $managedUiProcess) { $managedUiProcess.Id } else { $null }
            appSettingsPath = $isolatedAppSettingsPath
        }
        runtime = [PSCustomObject]@{
            statusBeforeExports = $responses.statusBeforeExports
            status = $responses.status
            selection = $responses.selection
            notch = $responses.notch
            notchValidation = $responses.notchValidation
        }
        exports = $artifactRows
        goldenChecks = $goldenChecks
        signedInputs = $signedInputs
        signedActuals = $signedActuals
        exportStateInvariant = [PSCustomObject]@{
            pass = $true
            before = $exportStateBefore
            after = $exportStateAfter
        }
        selectionLatency = [PSCustomObject]@{
            sampleCadIds = $selectionStats.sampleCadIds
            cycles = $selectionStats.cycles
            sampleCount = $selectionStats.sampleCount
            totalMs = $selectionStats.totalMs
            inspectorMs = $selectionStats.inspectorMs
            notchPreviewMs = $selectionStats.notchPreviewMs
        }
        budget = $budget
        gate = $budgetGate
    }

    $summaryJsonPath = Join-Path $resolvedOutDir "regression-baseline-summary.json"
    Save-JsonFile -Path $summaryJsonPath -Data $summary

    $summaryMarkdown = New-Object System.Collections.Generic.List[string]
    $summaryMarkdown.Add("# 3635 Regression Baseline (Latest)")
    $summaryMarkdown.Add(("Generated: {0}" -f $generatedUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")))
    $summaryMarkdown.Add(("- Project: {0}" -f $ProjectPath))
    $summaryMarkdown.Add(("- CAD id: {0}" -f $CadId))
    $summaryMarkdown.Add(("- Regular id (notch-validation): {0}" -f $resolvedRegularId))
    $summaryMarkdown.Add("")
    $summaryMarkdown.Add("## Workflow")
    $summaryMarkdown.Add(("- load-project: {0}" -f $responses.loadProject.data.statusText))
    $summaryMarkdown.Add(("- step1: {0}" -f $responses.runStep1.data.statusText))
    $summaryMarkdown.Add(("- step2: {0}" -f $responses.runStep2.data.statusText))
    $summaryMarkdown.Add(("- step3: {0}" -f $responses.runStep3.data.statusText))
    $summaryMarkdown.Add(("- step4: {0}" -f $responses.runStep4.data.statusText))
    $summaryMarkdown.Add("")
    $summaryMarkdown.Add("## Step 5 Exports")
    foreach ($row in $artifactRows) {
        $summaryMarkdown.Add(("- {0}: exists={1}, size={2}, elapsedMs={3}, path={4}" -f $row.format, $row.exists, $row.size, $row.elapsedMs, $row.path))
    }
    $summaryMarkdown.Add("")
    $summaryMarkdown.Add("## Golden Export Gate")
    $summaryMarkdown.Add(("- export state invariant: PASS; V21={0}; V22={1}; fileType={2}; profile={3}" -f
        $exportStateAfter.enableV21,
        $exportStateAfter.enableV22,
        $exportStateAfter.fileType,
        $exportStateAfter.profile))
    foreach ($check in $goldenChecks) {
        $status = if ($check.skipped) { "SKIPPED" } elseif ($check.pass) { "PASS" } else { "FAIL" }
        $summaryMarkdown.Add(("- {0}: {1}; actual={2}; golden={3}" -f
            $check.format,
            $status,
            $check.actualPath,
            $check.goldenPath))
    }
    foreach ($signedActual in $signedActuals) {
        $summaryMarkdown.Add(("- {0}: nodes={1}; bytes={2}; SHA-256={3}" -f
            $signedActual.label,
            $signedActual.nodes,
            $signedActual.bytes,
            $signedActual.sha256))
    }
    $summaryMarkdown.Add("")
    $summaryMarkdown.Add("## Runtime Query Baseline Files")
    foreach ($fileName in @(
            "runtime-status.json",
            "runtime-status-before-exports.json",
            "runtime-selection.json",
            "runtime-notch-stage.json",
            "runtime-notch.json",
            "runtime-notch-validation.json",
            "runtime-export-csv.json",
            "runtime-export-c-v21.json",
            "runtime-export-c-v22.json",
            "selection-latency.json")) {
        $summaryMarkdown.Add(("- {0}" -f (Join-Path $OutDir $fileName)))
    }
    $summaryMarkdown.Add("")
    $summaryMarkdown.Add("## Selection Latency")
    $summaryMarkdown.Add(("- sample count: {0}" -f $selectionStats.sampleCount))
    $summaryMarkdown.Add("| Metric | Count | Min | p50 | p95 | Max | Avg |")
    $summaryMarkdown.Add("|---|---:|---:|---:|---:|---:|---:|")
    $summaryMarkdown.Add(("| totalMs | {0} | {1} | {2} | {3} | {4} | {5} |" -f
        $selectionStats.totalMs.count,
        $selectionStats.totalMs.min,
        $selectionStats.totalMs.p50,
        $selectionStats.totalMs.p95,
        $selectionStats.totalMs.max,
        $selectionStats.totalMs.avg))
    $summaryMarkdown.Add(("| inspectorMs | {0} | {1} | {2} | {3} | {4} | {5} |" -f
        $selectionStats.inspectorMs.count,
        $selectionStats.inspectorMs.min,
        $selectionStats.inspectorMs.p50,
        $selectionStats.inspectorMs.p95,
        $selectionStats.inspectorMs.max,
        $selectionStats.inspectorMs.avg))
    $summaryMarkdown.Add(("| notchPreviewMs | {0} | {1} | {2} | {3} | {4} | {5} |" -f
        $selectionStats.notchPreviewMs.count,
        $selectionStats.notchPreviewMs.min,
        $selectionStats.notchPreviewMs.p50,
        $selectionStats.notchPreviewMs.p95,
        $selectionStats.notchPreviewMs.max,
        $selectionStats.notchPreviewMs.avg))
    $summaryMarkdown.Add("")
    $summaryMarkdown.Add("## Budget Gate")
    $summaryMarkdown.Add(("- budget file: {0}" -f $BudgetPath))
    $summaryMarkdown.Add(("- pass: {0}" -f $budgetGate.pass))
    $summaryMarkdown.Add("| Check | Actual | Budget | Pass |")
    $summaryMarkdown.Add("|---|---:|---:|:---:|")
    foreach ($check in $budgetGate.checks) {
        $summaryMarkdown.Add(("| {0} | {1} {2} | {3} {2} | {4} |" -f
            $check.name,
            $check.actual,
            $check.unit,
            $check.budget,
            ($(if ($check.pass) { "PASS" } else { "FAIL" }))))
    }

    $summaryMarkdownPath = Join-Path $resolvedOutDir "regression-baseline-summary.md"
    Set-Content -LiteralPath $summaryMarkdownPath -Value $summaryMarkdown -Encoding utf8

    Write-Host ("Regression baseline summary generated: {0}" -f $summaryMarkdownPath) -ForegroundColor Green
    Write-Host ("Regression baseline json generated: {0}" -f $summaryJsonPath) -ForegroundColor Green
    if ($EnforceBudget -and -not $budgetGate.pass) {
        $failedSummary = ($budgetGate.failed | ForEach-Object { "{0}: actual={1}{2}, budget={3}{2}" -f $_.name, $_.actual, $_.unit, $_.budget }) -join "; "
        throw "3635 regression budget failed: $failedSummary"
    }
}
finally {
    try {
        if ($null -ne $managedUiProcess) {
            $managedUiProcess.Refresh()
            if (-not $managedUiProcess.HasExited) {
                $expectedUiPath = [System.IO.Path]::GetFullPath($uiExe)
                $actualUiPath = [System.IO.Path]::GetFullPath($managedUiProcess.Path)
                if ($actualUiPath -ne $expectedUiPath) {
                    throw "Refusing to stop managed UI PID $($managedUiProcess.Id) because its path is '$actualUiPath'."
                }

                Stop-Process -Id $managedUiProcess.Id -Force
                Write-Host ("Stopped isolated UI instance (PID {0})." -f $managedUiProcess.Id) -ForegroundColor DarkGray
            }
        }
    }
    finally {
        if ($settingsOverrideWasSet) {
            [System.Environment]::SetEnvironmentVariable(
                $settingsOverrideName,
                $previousSettingsOverride,
                [System.EnvironmentVariableTarget]::Process)
        }

        Pop-Location
    }
}
