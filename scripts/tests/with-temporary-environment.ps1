function Invoke-WithTemporaryEnvironmentVariables {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary]$Settings,
        [Parameter(Mandatory = $true)]
        [scriptblock]$Action
    )

    $previous = @{}
    try {
        foreach ($name in $Settings.Keys) {
            $previous[$name] = @{
                Exists = Test-Path -LiteralPath "Env:$name"
                Value = [Environment]::GetEnvironmentVariable($name)
            }
            [Environment]::SetEnvironmentVariable($name, [string]$Settings[$name])
        }

        & $Action
    }
    finally {
        foreach ($name in $previous.Keys) {
            if ($previous[$name].Exists) {
                [Environment]::SetEnvironmentVariable($name, $previous[$name].Value)
            } else {
                Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
            }
        }
    }
}
