param(
    [string]$ProjectPath = "",
    [string]$UiProject = "src/FreeformHelper.UI/FreeformHelper.UI.csproj",
    [string]$ExePath = "build/bin/FreeformHelper.UI/Debug/net10.0/FreeformHelper.UI.exe",
    [int]$StartupTimeoutMs = 60000,
    [int]$LoadTimeoutMs = 120000,
    [int]$PollIntervalMs = 400,
    [switch]$SkipBuild,
    [string]$OutPath = "build/perf/startup-load-latest.md",
    [string]$OutJsonPath = "build/perf/startup-load-latest.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-RepoPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BasePath,
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $Path))
}

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

function Convert-ToIntSafe {
    param(
        [Parameter(Mandatory = $false)]
        [object]$Value,
        [int]$DefaultValue = 0
    )

    if ($null -eq $Value) {
        return $DefaultValue
    }

    $parsed = 0
    if ([int]::TryParse($Value.ToString(), [ref]$parsed)) {
        return $parsed
    }

    return $DefaultValue
}

function Convert-ToBoolSafe {
    param(
        [Parameter(Mandatory = $false)]
        [object]$Value,
        [bool]$DefaultValue = $false
    )

    if ($null -eq $Value) {
        return $DefaultValue
    }

    $parsed = $false
    if ([bool]::TryParse($Value.ToString(), [ref]$parsed)) {
        return $parsed
    }

    return $DefaultValue
}

function Stop-FreeformProcess {
    Get-Process FreeformHelper.UI -ErrorAction SilentlyContinue | Stop-Process -Force
}

function Convert-QueryJson {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RawText
    )

    $text = [System.Text.RegularExpressions.Regex]::Replace(
        $RawText,
        "\x1B\[[0-9;?]*[ -/]*[@-~]",
        [string]::Empty).Trim()
    if ([string]::IsNullOrWhiteSpace($text)) {
        throw "Runtime query returned empty output."
    }

    try {
        return $text | ConvertFrom-Json
    }
    catch {
        $firstBrace = $text.IndexOf("{", [System.StringComparison]::Ordinal)
        $lastBrace = $text.LastIndexOf("}", [System.StringComparison]::Ordinal)
        if ($firstBrace -ge 0 -and $lastBrace -gt $firstBrace) {
            $candidate = $text.Substring($firstBrace, $lastBrace - $firstBrace + 1)
            return $candidate | ConvertFrom-Json
        }

        throw
    }
}

function Invoke-FreeformQuery {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExeFullPath,
        [Parameter(Mandatory = $true)]
        [string[]]$QueryArgs,
        [int]$TimeoutMs = 3000
    )

    if ($QueryArgs.Length -eq 0) {
        throw "QueryArgs cannot be empty."
    }

    $safeTimeout = [Math]::Max(1, [Math]::Min(120000, $TimeoutMs))
    $command = $QueryArgs[0]
    $argMap = @{}
    $index = 1
    while ($index -lt $QueryArgs.Length) {
        $token = $QueryArgs[$index]
        if (-not $token.StartsWith("--", [System.StringComparison]::Ordinal)) {
            $index++
            continue
        }

        $key = $token.Substring(2)
        $value = "true"
        if (($index + 1) -lt $QueryArgs.Length -and -not $QueryArgs[$index + 1].StartsWith("--", [System.StringComparison]::Ordinal)) {
            $value = $QueryArgs[$index + 1]
            $index += 2
        }
        else {
            $index++
        }

        $argMap[$key] = $value
    }

    $client = [System.IO.Pipes.NamedPipeClientStream]::new(
        ".",
        "freeformhelper.runtime.v1",
        [System.IO.Pipes.PipeDirection]::InOut,
        [System.IO.Pipes.PipeOptions]::Asynchronous)

    try {
        $client.Connect($safeTimeout)

        $writer = [System.IO.StreamWriter]::new(
            $client,
            [System.Text.UTF8Encoding]::new($false),
            4096,
            $true)
        $writer.AutoFlush = $true

        $reader = [System.IO.StreamReader]::new(
            $client,
            [System.Text.Encoding]::UTF8,
            $false,
            4096,
            $true)

        $request = [ordered]@{
            version = "1"
            command = $command
            args = if ($argMap.Count -gt 0) { $argMap } else { $null }
        }

        $requestJson = $request | ConvertTo-Json -Depth 8 -Compress
        $writer.WriteLine($requestJson)
        $responseJson = $reader.ReadLine()
        if ([string]::IsNullOrWhiteSpace($responseJson)) {
            throw "Runtime query returned empty response."
        }

        $response = $responseJson | ConvertFrom-Json
        return [PSCustomObject]@{
            ExitCode = if ($response.ok) { 0 } else { 1 }
            Response = $response
            Raw = $responseJson
        }
    }
    catch {
        return [PSCustomObject]@{
            ExitCode = 1
            Response = [PSCustomObject]@{
                ok = $false
                data = $null
                error = [PSCustomObject]@{
                    code = "PIPE_ERROR"
                    message = $_.Exception.Message
                }
            }
            Raw = $_.Exception.Message
        }
    }
    finally {
        $client.Dispose()
    }
}

function Wait-ForRuntimeReady {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExeFullPath,
        [int]$TimeoutMs = 60000,
        [int]$PollMs = 400
    )

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $queryTimeoutMs = [Math]::Max(150, [Math]::Min(3000, $PollMs))
    while ($sw.ElapsedMilliseconds -lt $TimeoutMs) {
        try {
            $result = Invoke-FreeformQuery -ExeFullPath $ExeFullPath -QueryArgs @("status") -TimeoutMs $queryTimeoutMs
            if ($result.ExitCode -eq 0 -and $result.Response.ok) {
                return [PSCustomObject]@{
                    Ready = $true
                    ElapsedMs = $sw.ElapsedMilliseconds
                    Status = $result.Response.data
                }
            }
        }
        catch {
            # ignore and retry until timeout
        }

        Start-Sleep -Milliseconds $PollMs
    }

    return [PSCustomObject]@{
        Ready = $false
        ElapsedMs = $sw.ElapsedMilliseconds
        Status = $null
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\\..")
Push-Location $repoRoot
try {
    $uiProjectFullPath = Resolve-RepoPath -BasePath $repoRoot -Path $UiProject
    $exeFullPath = Resolve-RepoPath -BasePath $repoRoot -Path $ExePath
    $outFullPath = Resolve-RepoPath -BasePath $repoRoot -Path $OutPath
    $outJsonFullPath = Resolve-RepoPath -BasePath $repoRoot -Path $OutJsonPath

    if (-not $SkipBuild) {
        dotnet build $uiProjectFullPath | Out-Host
    }

    if (-not (Test-Path -LiteralPath $exeFullPath)) {
        throw "Executable not found: $exeFullPath. Build first or pass -ExePath."
    }

    Stop-FreeformProcess
    $previousPerfMarkerEnv = $env:FREEFORM_PERF_MARKERS_INFO
    $env:FREEFORM_PERF_MARKERS_INFO = "1"
    $appProcess = $null
    try {
        $appProcess = Start-Process -FilePath $exeFullPath -PassThru
    }
    finally {
        if ($null -eq $previousPerfMarkerEnv) {
            Remove-Item Env:FREEFORM_PERF_MARKERS_INFO -ErrorAction SilentlyContinue
        }
        else {
            $env:FREEFORM_PERF_MARKERS_INFO = $previousPerfMarkerEnv
        }
    }
    $readyResult = Wait-ForRuntimeReady -ExeFullPath $exeFullPath -TimeoutMs $StartupTimeoutMs -PollMs $PollIntervalMs
    if (-not $readyResult.Ready) {
        throw "Runtime not ready within ${StartupTimeoutMs}ms."
    }

    $loadResult = $null
    $projectFullPath = $null
    if (-not [string]::IsNullOrWhiteSpace($ProjectPath)) {
        $projectFullPath = Resolve-RepoPath -BasePath $repoRoot -Path $ProjectPath
        if (-not (Test-Path -LiteralPath $projectFullPath)) {
            throw "Project file not found: $projectFullPath"
        }

        $query = Invoke-FreeformQuery -ExeFullPath $exeFullPath -QueryArgs @("load-project", "--path", $projectFullPath) -TimeoutMs $LoadTimeoutMs
        if ($query.ExitCode -ne 0 -or -not $query.Response.ok) {
            throw "load-project failed: $($query.Raw)"
        }

        $loadData = $query.Response.data
        $timings = Get-OptionalPropertyValue -Object $loadData -Name "timings"
        $stepReplay = Get-OptionalPropertyValue -Object $timings -Name "stepReplay"
        $loadResult = [PSCustomObject]@{
            path = (Get-OptionalPropertyValue -Object $loadData -Name "path" -DefaultValue $projectFullPath)
            statusText = (Get-OptionalPropertyValue -Object $loadData -Name "statusText" -DefaultValue [string]::Empty)
            isLoaded = [bool]$query.Response.ok
            totalElapsedMs = Convert-ToIntSafe (Get-OptionalPropertyValue -Object $timings -Name "TotalElapsedMs")
            persistenceElapsedMs = Convert-ToIntSafe (Get-OptionalPropertyValue -Object $timings -Name "PersistenceElapsedMs")
            applyStateElapsedMs = Convert-ToIntSafe (Get-OptionalPropertyValue -Object $timings -Name "ApplyStateElapsedMs")
            dxfElapsedMs = Convert-ToIntSafe (Get-OptionalPropertyValue -Object $timings -Name "DxfElapsedMs")
            rebuildElapsedMs = Convert-ToIntSafe (Get-OptionalPropertyValue -Object $timings -Name "RebuildElapsedMs")
            step1ReplayElapsedMs = Convert-ToIntSafe (Get-OptionalPropertyValue -Object $stepReplay -Name "Step1ReplayElapsedMs")
            step2ReplayElapsedMs = Convert-ToIntSafe (Get-OptionalPropertyValue -Object $stepReplay -Name "Step2ReplayElapsedMs")
            step1Replayed = Convert-ToBoolSafe (Get-OptionalPropertyValue -Object $stepReplay -Name "Step1Replayed")
            step2Replayed = Convert-ToBoolSafe (Get-OptionalPropertyValue -Object $stepReplay -Name "Step2Replayed")
            cadCount = Convert-ToIntSafe (Get-OptionalPropertyValue -Object $loadData -Name "cadCount")
            regularCount = Convert-ToIntSafe (Get-OptionalPropertyValue -Object $loadData -Name "regularCount")
        }
    }

    $statusQuery = Invoke-FreeformQuery -ExeFullPath $exeFullPath -QueryArgs @("status")
    $statusData = if ($statusQuery.ExitCode -eq 0 -and $statusQuery.Response.ok) { $statusQuery.Response.data } else { $null }

    $summary = [PSCustomObject]@{
        generatedUtc = [DateTime]::UtcNow.ToString("o")
        startup = [PSCustomObject]@{
            readyElapsedMs = $readyResult.ElapsedMs
            hasCad = if ($null -ne $readyResult.Status) { [bool]$readyResult.Status.workflow.hasCad } else { $false }
            hasGrid = if ($null -ne $readyResult.Status) { [bool]$readyResult.Status.workflow.hasGrid } else { $false }
        }
        loadProject = if ($null -ne $loadResult) {
            [PSCustomObject]@{
                path = $loadResult.path
                isLoaded = [bool]$loadResult.isLoaded
                totalElapsedMs = [int]$loadResult.totalElapsedMs
                persistenceElapsedMs = [int]$loadResult.persistenceElapsedMs
                applyStateElapsedMs = [int]$loadResult.applyStateElapsedMs
                dxfElapsedMs = [int]$loadResult.dxfElapsedMs
                rebuildElapsedMs = [int]$loadResult.rebuildElapsedMs
                step1ReplayElapsedMs = [int]$loadResult.step1ReplayElapsedMs
                step2ReplayElapsedMs = [int]$loadResult.step2ReplayElapsedMs
                step1Replayed = [bool]$loadResult.step1Replayed
                step2Replayed = [bool]$loadResult.step2Replayed
                statusText = [string]$loadResult.statusText
                cadCount = [int]$loadResult.cadCount
                regularCount = [int]$loadResult.regularCount
            }
        } else {
            $null
        }
        finalStatus = $statusData
    }

    $markdown = New-Object System.Collections.Generic.List[string]
    $markdown.Add("# Startup/Load Measurement (Latest)")
    $markdown.Add(("Generated: {0}" -f ([DateTime]::UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"))))
    $markdown.Add(("Executable: {0}" -f $exeFullPath))
    $markdown.Add("")
    $markdown.Add("## Startup")
    $markdown.Add(("- Runtime ready: {0} ms" -f $summary.startup.readyElapsedMs))
    $markdown.Add(("- Workflow ready at probe: hasCad={0}, hasGrid={1}" -f $summary.startup.hasCad, $summary.startup.hasGrid))
    if ($summary.loadProject -ne $null) {
        $markdown.Add("")
        $markdown.Add("## Load Project")
        $markdown.Add(("- Path: {0}" -f $summary.loadProject.path))
        $markdown.Add(("- Status: {0}" -f $summary.loadProject.statusText))
        $markdown.Add(("- Counts: cad={0}, regular={1}" -f $summary.loadProject.cadCount, $summary.loadProject.regularCount))
        $markdown.Add(("- Total: {0} ms (persistence={1}, apply={2}, dxf={3}, rebuild={4}, step1={5}, step2={6})" -f
            $summary.loadProject.totalElapsedMs,
            $summary.loadProject.persistenceElapsedMs,
            $summary.loadProject.applyStateElapsedMs,
            $summary.loadProject.dxfElapsedMs,
            $summary.loadProject.rebuildElapsedMs,
            $summary.loadProject.step1ReplayElapsedMs,
            $summary.loadProject.step2ReplayElapsedMs))
        $markdown.Add(("- Replay flags: step1={0}, step2={1}" -f $summary.loadProject.step1Replayed, $summary.loadProject.step2Replayed))
    }

    $outDir = Split-Path -Parent $outFullPath
    if (-not [string]::IsNullOrWhiteSpace($outDir) -and -not (Test-Path -LiteralPath $outDir)) {
        New-Item -Path $outDir -ItemType Directory -Force | Out-Null
    }

    $jsonDir = Split-Path -Parent $outJsonFullPath
    if (-not [string]::IsNullOrWhiteSpace($jsonDir) -and -not (Test-Path -LiteralPath $jsonDir)) {
        New-Item -Path $jsonDir -ItemType Directory -Force | Out-Null
    }

    Set-Content -LiteralPath $outFullPath -Value $markdown -Encoding utf8
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outJsonFullPath -Encoding utf8

    Write-Host "Startup/load markdown generated: $outFullPath"
    Write-Host "Startup/load json generated: $outJsonFullPath"
}
finally {
    Stop-FreeformProcess
    Pop-Location
}
