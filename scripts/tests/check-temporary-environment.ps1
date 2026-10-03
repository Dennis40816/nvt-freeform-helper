Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "with-temporary-environment.ps1")
$suffix = [Guid]::NewGuid().ToString('N')
$setName = "FREEFORMHELPER_TEMP_ENV_SET_$suffix"
$unsetName = "FREEFORMHELPER_TEMP_ENV_UNSET_$suffix"
[Environment]::SetEnvironmentVariable($setName, 'before')
Remove-Item -LiteralPath "Env:$unsetName" -ErrorAction SilentlyContinue

try {
    try {
        Invoke-WithTemporaryEnvironmentVariables -Settings @{
            $setName = 'during'
            $unsetName = 'during'
        } -Action {
            if ([Environment]::GetEnvironmentVariable($setName) -cne 'during' -or
                [Environment]::GetEnvironmentVariable($unsetName) -cne 'during') {
                throw 'Temporary environment variables were not applied.'
            }

            throw 'Expected test exception.'
        }
    }
    catch {
        if ($_.Exception.Message -cne 'Expected test exception.') {
            throw
        }
    }

    if ([Environment]::GetEnvironmentVariable($setName) -cne 'before') {
        throw 'The previously set variable was not restored.'
    }

    if (Test-Path -LiteralPath "Env:$unsetName") {
        throw 'The previously unset variable still exists.'
    }

    Write-Host '[check-temporary-environment] PASS: set and unset variables restored after an exception.'
}
finally {
    Remove-Item -LiteralPath "Env:$setName" -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath "Env:$unsetName" -ErrorAction SilentlyContinue
}
