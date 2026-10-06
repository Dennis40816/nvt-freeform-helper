param(
    [string]$ProjectPath = "example/BOE36.35/project_3635.json",
    [int]$ReadyTimeoutMs = 180000,
    [int]$QueryTimeoutMs = 120000,
    [int]$RetryCount = 24,
    [int]$RetryDelayMs = 500,
    [switch]$BuildIfNeeded
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$uiProject = Join-Path $repoRoot "src/FreeformHelper.UI/FreeformHelper.UI.csproj"
$uiExe = Join-Path $repoRoot "build/bin/FreeformHelper.UI/Debug/net10.0/FreeformHelper.UI.exe"

function Convert-QueryResponse {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$RawText
    )

    $cleanText = [System.Text.RegularExpressions.Regex]::Replace(
        $RawText,
        "\x1B\[[0-9;?]*[ -/]*[@-~]",
        [string]::Empty)
    $cleanText = [System.Text.RegularExpressions.Regex]::Replace(
        $cleanText,
        "[\x00-\x1F]",
        [string]::Empty).Trim()
    if ([string]::IsNullOrWhiteSpace($cleanText)) {
        return [PSCustomObject]@{
            ok = $false
            error = [PSCustomObject]@{
                code = "EMPTY_RESPONSE"
                message = "Runtime query returned empty output."
            }
        }
    }

    $candidate = $cleanText
    $firstBrace = $cleanText.IndexOf("{", [System.StringComparison]::Ordinal)
    $lastBrace = $cleanText.LastIndexOf("}", [System.StringComparison]::Ordinal)
    if ($firstBrace -ge 0 -and $lastBrace -gt $firstBrace) {
        $candidate = $cleanText.Substring($firstBrace, $lastBrace - $firstBrace + 1)
    }

    try {
        return $candidate | ConvertFrom-Json
    }
    catch {
        return [PSCustomObject]@{
            ok = $false
            error = [PSCustomObject]@{
                code = "INVALID_JSON"
                message = $_.Exception.Message
            }
        }
    }
}

function Invoke-RuntimeQuery {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command,
        [string[]]$Args = @(),
        [string[]]$RetryCodes = @()
    )

    for ($attempt = 0; $attempt -le $RetryCount; $attempt++) {
        $response = $null
        try {
            $allArgs = @("query", $Command) + $Args + @("--timeout-ms", $QueryTimeoutMs.ToString([System.Globalization.CultureInfo]::InvariantCulture), "--json-compact")
            $raw = if (Test-Path -LiteralPath $uiExe) {
                (& $uiExe @allArgs 2>&1 | Out-String)
            }
            else {
                (& dotnet run --project $uiProject -- @allArgs 2>&1 | Out-String)
            }

            $response = Convert-QueryResponse -RawText $raw
        }
        catch {
            $response = [PSCustomObject]@{
                ok = $false
                error = [PSCustomObject]@{
                    code = "PROCESS_ERROR"
                    message = $_.Exception.Message
                }
            }
        }

        if ($response.ok) {
            return $response
        }

        $errorCode = [string]$response.error.code
        $errorMessage = [string]$response.error.message
        $canRetry = $attempt -lt $RetryCount -and $RetryCodes -contains $errorCode
        if ($canRetry) {
            Write-Warning ("Runtime query retry {0}/{1}: {2} - {3}" -f ($attempt + 1), ($RetryCount + 1), $errorCode, $errorMessage)
            Start-Sleep -Milliseconds $RetryDelayMs
            continue
        }

        throw "Runtime query failed: command=$Command code=$errorCode message=$errorMessage"
    }

    throw "Runtime query retry exhausted: command=$Command"
}

function Ensure-UiInstanceRunning {
    $existingProcesses = @(Get-Process FreeformHelper.UI -ErrorAction SilentlyContinue)
    if ($existingProcesses.Count -gt 0) {
        Write-Host ("Detected {0} existing FreeformHelper.UI process(es). Reusing current instance." -f $existingProcesses.Count) -ForegroundColor Cyan
        return
    }

    if ($BuildIfNeeded -and -not (Test-Path -LiteralPath $uiExe)) {
        Write-Host "UI executable not found. Building UI project..." -ForegroundColor Cyan
        dotnet build $uiProject --nologo /p:UseAppHost=false
    }

    Write-Host "Starting FreeformHelper UI..." -ForegroundColor Cyan
    if (Test-Path -LiteralPath $uiExe) {
        Start-Process -FilePath $uiExe -WorkingDirectory $repoRoot | Out-Null
    }
    else {
        Start-Process -FilePath "dotnet" -ArgumentList @("run", "--project", $uiProject) -WorkingDirectory $repoRoot | Out-Null
    }

    Start-Sleep -Milliseconds 4000
}

Push-Location $repoRoot
try {
    Ensure-UiInstanceRunning
    Invoke-RuntimeQuery -Command "status" -RetryCodes @("INSTANCE_NOT_RUNNING", "IPC_IO_ERROR", "IPC_ERROR", "PROCESS_ERROR", "EMPTY_RESPONSE", "INVALID_JSON") | Out-Null

    $resolvedProjectPath = if ([System.IO.Path]::IsPathRooted($ProjectPath)) {
        $ProjectPath
    }
    else {
        Join-Path $repoRoot $ProjectPath
    }

    if (-not (Test-Path -LiteralPath $resolvedProjectPath)) {
        throw "Project file not found: $resolvedProjectPath"
    }

    Write-Host "Loading project and opening verification (Step4 diagnostics)..." -ForegroundColor Cyan
    $loadResponse = Invoke-RuntimeQuery -Command "load-project" -Args @("--path", $resolvedProjectPath) -RetryCodes @("INSTANCE_NOT_RUNNING", "IPC_IO_ERROR", "IPC_ERROR", "PROCESS_ERROR", "EMPTY_RESPONSE", "INVALID_JSON")
    $statusAfterLoad = Invoke-RuntimeQuery -Command "status" -RetryCodes @("PROCESS_ERROR", "EMPTY_RESPONSE", "INVALID_JSON")

    if (-not [bool]$statusAfterLoad.data.workflow.hasStep1Result) {
        Invoke-RuntimeQuery -Command "run-step" -Args @("--step", "1") -RetryCodes @("STEP_NOT_READY", "NOT_READY", "PROCESS_ERROR", "EMPTY_RESPONSE", "INVALID_JSON") | Out-Null
    }

    Invoke-RuntimeQuery -Command "run-step" -Args @("--step", "4") -RetryCodes @("STEP_NOT_READY", "NOT_READY", "PROCESS_ERROR", "EMPTY_RESPONSE", "INVALID_JSON") | Out-Null
    $finalStatus = Invoke-RuntimeQuery -Command "status" -RetryCodes @("PROCESS_ERROR", "EMPTY_RESPONSE", "INVALID_JSON")

    Write-Host ("Loaded: {0}" -f $loadResponse.data.path) -ForegroundColor Green
    Write-Host ("Status: {0}" -f $finalStatus.data.statusText) -ForegroundColor Green
    Write-Host "Done. BOE36.35 project is loaded and verification page should be open." -ForegroundColor Green
}
finally {
    Pop-Location
}
