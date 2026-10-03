param(
    [string]$Project,
    [string]$Steps = "1,2,3",
    [int]$CadId = -1,
    [int]$TimeoutMs = 120000,
    [switch]$Compact,
    [int]$RetryCount = 12,
    [int]$RetryDelayMs = 350,
    [int]$ReadyTimeoutMs = 60000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$uiProject = "src/FreeformHelper.UI/FreeformHelper.UI.csproj"
$uiExe = Join-Path $repoRoot "build/bin/FreeformHelper.UI/Debug/net8.0/FreeformHelper.UI.exe"
$runtimeCmd = if (Test-Path -LiteralPath $uiExe) { $uiExe } else { "dotnet" }
$runtimePrefix = if (Test-Path -LiteralPath $uiExe) { @() } else { @("run", "--project", $uiProject, "--") }

function Write-JsonResponse {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Response
    )

    if ($Compact) {
        $Response | ConvertTo-Json -Depth 100 -Compress
    }
    else {
        $Response | ConvertTo-Json -Depth 100
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

function Parse-Steps {
    param(
        [string]$Raw
    )

    $tokens = $Raw.Split(",", [System.StringSplitOptions]::RemoveEmptyEntries)
    $steps = New-Object System.Collections.Generic.List[int]
    foreach ($token in $tokens) {
        $trimmed = $token.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) {
            continue
        }

        $parsedValue = 0
        if (-not [int]::TryParse($trimmed, [ref]$parsedValue)) {
            throw "Invalid step token '$trimmed'. Use comma-separated integers, e.g. 1,2,3."
        }

        $value = $parsedValue
        if ($value -lt 1 -or $value -gt 4) {
            throw "Unsupported step '$value'. Supported steps: 1, 2, 3, 4."
        }

        if (-not $steps.Contains($value)) {
            $steps.Add($value)
        }
    }

    return $steps
}

Push-Location $repoRoot
try {
    if (-not [string]::IsNullOrWhiteSpace($Project)) {
        Invoke-RuntimeQuery -QueryArgs @("load-project", "--path", $Project) -RetryErrorCodes @("INSTANCE_NOT_RUNNING", "IPC_IO_ERROR", "IPC_ERROR") | Out-Null
        Wait-Workflow -Description "workflow has CAD+grid after load-project" -Predicate {
            param($statusData)
            return [bool]$statusData.workflow.hasCad -and [bool]$statusData.workflow.hasGrid
        } | Out-Null
    }

    $stepsToRun = Parse-Steps -Raw $Steps
    $cadIdText = if ($CadId -ge 0) { $CadId.ToString([System.Globalization.CultureInfo]::InvariantCulture) } else { $null }
    foreach ($step in $stepsToRun) {
        if ($step -eq 2) {
            Wait-Workflow -Description "step1 ready before step2" -Predicate {
                param($statusData)
                return [bool]$statusData.workflow.hasStep1Result
            } | Out-Null
        }
        elseif ($step -eq 4) {
            Wait-Workflow -Description "step1 ready before step4" -Predicate {
                param($statusData)
                return [bool]$statusData.workflow.hasStep1Result
            } | Out-Null
        }

        if ($step -eq 3) {
            if ($CadId -lt 0) {
                Write-Warning "Step 3 requires selected CAD. Skipped (use -CadId)."
                continue
            }

            Invoke-RuntimeQuery -QueryArgs @("select-cad", "--cad-id", $cadIdText) -RetryErrorCodes @("STEP_NOT_READY", "NOT_READY") | Out-Null
            Wait-Workflow -Description "CAD selection is applied before step3" -Predicate {
                param($statusData)
                return [int]$statusData.selection.cadCount -ge 1
            } | Out-Null
        }

        Invoke-RuntimeQuery -QueryArgs @("run-step", "--step", $step.ToString([System.Globalization.CultureInfo]::InvariantCulture)) -RetryErrorCodes @("STEP_NOT_READY", "NOT_READY") | Out-Null

        if ($step -eq 1) {
            Wait-Workflow -Description "step1 result ready" -Predicate {
                param($statusData)
                return [bool]$statusData.workflow.hasStep1Result
            } | Out-Null
        }
        elseif ($step -eq 2) {
            Wait-Workflow -Description "step2 result ready" -Predicate {
                param($statusData)
                return [bool]$statusData.workflow.hasStep2Result
            } | Out-Null
        }
        elseif ($step -eq 4) {
            Wait-Workflow -Description "step4 result ready" -Predicate {
                param($statusData)
                return [bool]$statusData.workflow.hasStep4Result
            } | Out-Null
        }
    }

    if ($CadId -ge 0) {
        Invoke-RuntimeQuery -QueryArgs @("select-cad", "--cad-id", $cadIdText) -RetryErrorCodes @("STEP_NOT_READY", "NOT_READY") | Out-Null
        Invoke-RuntimeQuery -QueryArgs @("notch-stage", "--cad-id", $cadIdText, "--polygon-limit", "96") -RetryErrorCodes @("NOT_READY", "STEP_NOT_READY") | Out-Null
        Invoke-RuntimeQuery -QueryArgs @("notch", "--cad-id", $cadIdText, "--limit", "300", "--polygon-limit", "128") -RetryErrorCodes @("NOT_READY", "STEP_NOT_READY") | Out-Null
    }

    Invoke-RuntimeQuery -QueryArgs @("status") -RetryErrorCodes @("INSTANCE_NOT_RUNNING", "IPC_IO_ERROR", "IPC_ERROR") | Out-Null
}
finally {
    Pop-Location
}
