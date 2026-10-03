Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "private-path-patterns.ps1")
$patterns = @(Get-PrivatePathPatterns)
$backslash = '\'
$windowsRoot = 'C:' + $backslash + 'Users' + $backslash
$forwardRoot = 'C:/Users/'
$unixRoot = '/home/'
$gitBashRoot = '/c/' + 'Users' + '/'
$wslRoot = '/mnt/c/' + 'Users' + '/'

$mustHit = @(
    @{ Name = 'Windows profile'; Value = $windowsRoot + 'Alex' + $backslash + 'Documents' },
    @{ Name = 'forward slashes'; Value = $forwardRoot + 'Alex/Documents' },
    @{ Name = 'doubled backslashes'; Value = 'C:' + ($backslash * 2) + 'Users' + ($backslash * 2) + 'Alex' },
    @{ Name = 'CJK profile'; Value = $windowsRoot + '林小明' },
    @{ Name = 'Public followed by CJK'; Value = $windowsRoot + 'Public' + '林' },
    @{ Name = 'Public.backup'; Value = $windowsRoot + 'Public' + '.backup' },
    @{ Name = 'Public-old'; Value = $windowsRoot + 'Public' + '-old' },
    @{ Name = 'runneradmin2'; Value = $windowsRoot + 'runneradmin' + '2' },
    @{ Name = 'development root'; Value = 'C:' + $backslash + 'dennis' + $backslash + 'dev' },
    @{ Name = 'Unix home'; Value = $unixRoot + 'alex/Documents' },
    @{ Name = 'Git Bash home'; Value = $gitBashRoot + 'alex' },
    @{ Name = 'WSL home'; Value = $wslRoot + 'alex' }
)

$mustNotHit = @(
    @{ Name = 'Public'; Value = $windowsRoot + 'Public' },
    @{ Name = 'runneradmin'; Value = $windowsRoot + 'runneradmin' },
    @{ Name = 'CI workspace'; Value = $unixRoot + 'runner/work' },
    @{ Name = 'bare root in backticks'; Value = '`' + $windowsRoot + '`' },
    @{ Name = 'environment placeholder'; Value = $windowsRoot + '%USERNAME%' },
    @{ Name = 'brace placeholder'; Value = $windowsRoot + '{user}' },
    @{ Name = 'bare quoted user'; Value = '`' + 'liusx' },
    @{ Name = 'shell route placeholder'; Value = '/users/' + '${id}' },
    @{ Name = 'route parameter'; Value = '/users/' + ':id' },
    @{ Name = 'API URL'; Value = 'https://host.example/api/' + 'users/42' }
)

foreach ($allowedName in @('Public', 'runneradmin')) {
    foreach ($suffix in @('`', '(', ',', '.', '。')) {
        $mustNotHit += @{ Name = "$allowedName followed by $suffix"; Value = $windowsRoot + $allowedName + $suffix }
    }
}

foreach ($sample in $mustHit) {
    if (-not ($patterns | Where-Object { [regex]::IsMatch($sample.Value, $_) })) {
        throw "Private path pattern missed $($sample.Name)."
    }
}

foreach ($sample in $mustNotHit) {
    if ($patterns | Where-Object { [regex]::IsMatch($sample.Value, $_) }) {
        throw "Private path pattern matched $($sample.Name)."
    }
}

Write-Host "[check-private-path-patterns] PASS: $($mustHit.Count) must-hit and $($mustNotHit.Count) must-not-hit samples."
