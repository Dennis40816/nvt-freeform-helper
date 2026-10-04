#Requires -Version 7.0
[CmdletBinding()]
param([switch]$SelfTest)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-PathRule {
    param([string]$Text, [string]$UserProfilePath, [string]$UserName)

    $patterns = [ordered]@{
        'drive-root' = '(?i)(?<![\w])[a-z]:[\\/]'
        'unc' = '(?<![:/\\\w])(?:\\{2,}|//)(?:[\w.-]+|\?)[\\/]+[^\s/\\"'']+'
        'msys-drive' = '(?i)(?<![\w/\\.-])/[a-z]/'
        'wsl-drive' = '(?i)(?<![\w/\\.-])/mnt/[a-z]/'
        'users-root' = '(?i)(?<![\w/\\.-])/[U]sers/'
        'home-root' = '(?i)(?<![\w/\\.-])/[h]ome/'
    }
    foreach ($entry in $patterns.GetEnumerator()) {
        if ($Text -match $entry.Value) { $entry.Key }
    }

    # Normalize literal and source-escaped Windows separators before profile comparison.
    $normalized = $Text -replace '\\+', '/'
    if ($UserProfilePath) {
        $prefix = [regex]::Escape(($UserProfilePath -replace '\\+', '/').TrimEnd('/'))
        if ($normalized -match "(?i)(?<![\w/.-])$prefix(?=/|$|[`"',;\t])") { 'user-profile' }
    }
    if ($UserName) {
        $segment = [regex]::Escape($UserName)
        # A bare username or a substring of a directory name is not a path segment.
        if ($normalized -match "(?i)(?:/$segment(?=/|$|[`"',;\t])|(?<![\w/.-])$segment/)") {
            'user-name-segment'
        }
    }
}

function Get-Fingerprint {
    param([string]$Text)
    $hash = [System.Security.Cryptography.SHA256]::Create()
    try {
        [BitConverter]::ToString($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text))).Replace('-', '').ToLowerInvariant()
    }
    finally { $hash.Dispose() }
}

function Get-ValueFinding {
    param([string]$Text, [string]$Location, [string]$UserProfilePath, [string]$UserName)
    foreach ($rule in @(Get-PathRule $Text $UserProfilePath $UserName)) {
        [pscustomobject]@{ location = $Location; rule = $rule; fingerprint = Get-Fingerprint $Text }
    }
}

function Get-JsonFinding {
    param($Node, [string]$Location, [string]$UserProfilePath, [string]$UserName)
    switch ($Node.ValueKind) {
        'Object' {
            foreach ($property in $Node.EnumerateObject()) {
                $nameFindings = @(Get-ValueFinding $property.Name "$Location.*" $UserProfilePath $UserName)
                $nameFindings
                # A property name can itself contain a private path; never echo that name.
                $child = if ($nameFindings.Count) { "$Location.*" } else {
                    $escaped = $property.Name.Replace('\', '\\').Replace("'", "\'")
                    "$Location['$escaped']"
                }
                Get-JsonFinding $property.Value $child $UserProfilePath $UserName
            }
        }
        'Array' {
            $index = 0
            foreach ($item in $Node.EnumerateArray()) {
                Get-JsonFinding $item "$Location[$index]" $UserProfilePath $UserName
                $index++
            }
        }
        'String' { Get-ValueFinding $Node.GetString() $Location $UserProfilePath $UserName }
    }
}

function Get-ContentFinding {
    param([string]$Text, [bool]$IsJson, [string]$UserProfilePath, [string]$UserName)
    if ($IsJson) {
        $document = $null
        try {
            $document = [System.Text.Json.JsonDocument]::Parse($Text)
            Get-JsonFinding $document.RootElement '$' $UserProfilePath $UserName
        }
        catch { [pscustomobject]@{ location = '$'; rule = 'invalid-json'; fingerprint = '' } }
        finally { if ($null -ne $document) { $document.Dispose() } }
    }
    else {
        $line = 0
        foreach ($value in [regex]::Split($Text, '\r\n|\n|\r')) {
            $line++
            Get-ValueFinding $value "line:$line" $UserProfilePath $UserName
        }
    }
}

function Test-ExceptionValid {
    param($Exception, [datetime]$Today)
    if ($Exception -isnot [System.Collections.IDictionary]) { return $false }
    foreach ($key in @('file', 'rule', 'reason', 'owner', 'expiry')) {
        if ($Exception[$key] -isnot [string] -or [string]::IsNullOrWhiteSpace($Exception[$key])) { return $false }
    }
    if ($Exception.file -match '(^/|\\|:)' -or $Exception.file.Split('/') -contains '..') { return $false }
    if ($Exception.rule -notin @('drive-root', 'unc', 'msys-drive', 'wsl-drive', 'users-root', 'home-root', 'user-profile', 'user-name-segment')) { return $false }
    $field = $Exception['field']
    $fingerprint = $Exception['fingerprint']
    if (!$field -and !$fingerprint) { return $false }
    if ($field -and ($field -isnot [string] -or !$field.StartsWith('$') -or $field.Contains('*'))) { return $false }
    if ($fingerprint -and ($fingerprint -isnot [string] -or $fingerprint -notmatch '^[a-fA-F0-9]{64}$')) { return $false }
    $expiry = [datetime]::MinValue
    return [datetime]::TryParseExact($Exception.expiry, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::None, [ref]$expiry) -and $expiry.Date -ge $Today.Date
}

function Test-Excepted {
    param([string]$File, $Finding, [array]$Exceptions)
    foreach ($exception in $Exceptions) {
        if ($exception.file -cne $File -or $exception.rule -cne $Finding.rule) { continue }
        if ($exception['field'] -and $exception.field -cne $Finding.location) { continue }
        if ($exception['fingerprint'] -and $exception.fingerprint -ine $Finding.fingerprint) { continue }
        return $true
    }
    return $false
}

function Test-ScanFile {
    param([string]$File)
    if ($File -match '(^|/)(docs|\.git)/' -or $File -match '\.(md|rst)$') { return $false }
    $extension = [IO.Path]::GetExtension($File).ToLowerInvariant()
    if ($extension -in @('.json', '.csv', '.tsv', '.csproj', '.fsproj', '.vbproj', '.sln', '.slnx', '.props', '.targets', '.config', '.settings', '.runsettings', '.ini', '.toml', '.yaml', '.yml', '.xml', '.editorconfig')) { return $true }
    $textExtensions = @('.cs', '.fs', '.vb', '.ps1', '.psm1', '.py', '.sh', '.js', '.ts', '.c', '.h', '.txt', '.snap', '.snapshot', '.golden', '.axaml')
    return $extension -in $textExtensions -and $File -match '(^tests/|^scripts/tests/|^example/|(^|/)(golden|testdata|snapshots|baselines)(/|[._-]))'
}

function Write-Finding {
    param([string]$File, [string]$Location, [string]$Rule)
    # JSON output escapes control characters; no content, hash, or exception text is logged.
    Write-Host (ConvertTo-Json -Compress -InputObject ([ordered]@{ file = $File; location = $Location; rule = $Rule }))
}

if ($SelfTest) {
    # Synthetic inputs only: this branch precedes all config, Git, and repository reads.
    $drive = 'Q' + ':\work\mask.csv'
    $unc = ('\' * 2) + 'host\share\mask.csv'
    $UserProfilePath = '/var/profiles/guard-user'
    $samples = @(
        @{ text = $drive; rule = 'drive-root' }
        @{ text = 'z' + ':/mask.csv'; rule = 'drive-root' }
        @{ text = $unc; rule = 'unc' }
        @{ text = '"' + $unc.Replace('\', '\\') + '"'; rule = 'unc' }
        @{ text = '//' + 'host/share/mask.csv'; rule = 'unc' }
        @{ text = '/' + 'c/work/mask.csv'; rule = 'msys-drive' }
        @{ text = '/' + 'mnt/c/work/mask.csv'; rule = 'wsl-drive' }
        @{ text = '/' + 'Users/alice/mask.csv'; rule = 'users-root' }
        @{ text = '/' + 'home/alice/mask.csv'; rule = 'home-root' }
        @{ text = $UserProfilePath + '/mask.csv'; rule = 'user-profile' }
        @{ text = $UserProfilePath; rule = 'user-profile' }
        @{ text = $UserProfilePath + ',other'; rule = 'user-profile' }
        @{ text = 'fixtures/guard-user/mask.csv'; rule = 'user-name-segment' }
        @{ text = 'fixtures/GUARD-USER/mask.csv'; rule = 'user-name-segment' }
        @{ text = 'guard-user/mask.csv'; rule = 'user-name-segment' }
        @{ text = '"fixtures/guard-user"'; rule = 'user-name-segment' }
        @{ text = 'Q' + ':relative.csv'; rule = '' }
        @{ text = './mask.csv'; rule = '' }
        @{ text = '../mask.csv'; rule = '' }
        @{ text = 'assets/c/mask.csv'; rule = '' }
        @{ text = 'https://host/share'; rule = '' }
        @{ text = 'avares://App/Assets/x'; rule = '' }
        @{ text = '\\{2,}|//)[^\s/\\]'; rule = '' }
        @{ text = '/homework/x'; rule = '' }
        @{ text = '/UsersBackup/x'; rule = '' }
        @{ text = $UserProfilePath + '-backup/mask.csv'; rule = '' }
        @{ text = 'fixtures/my-guard-user/mask.csv'; rule = '' }
        @{ text = 'fixtures/guard-user-backup/mask.csv'; rule = '' }
        @{ text = 'guard-user'; rule = '' }
        @{ text = '%USERPROFILE%/mask.csv'; rule = '' }
        @{ text = '"' + $drive.Replace('\', '\\') + '"'; json = $true; rule = 'drive-root' }
        @{ text = '"' + $unc.Replace('\', '\\') + '"'; json = $true; rule = 'unc' }
        @{ text = '{"mask":"\u0051:\u005cwork\u005cmask.csv"}'; json = $true; rule = 'drive-root' }
        @{ text = '{"items":[{"mask":"\u002fhome\u002falice\u002fmask.csv"}]}'; json = $true; rule = 'home-root' }
        @{ text = '{"mask":"./mask.csv"}'; json = $true; rule = '' }
        @{ text = '{"mask":'; json = $true; rule = 'invalid-json' }
    )
    $failures = 0
    $checks = 0
    foreach ($sample in $samples) {
        $checks++
        $findings = @(Get-ContentFinding $sample.text ([bool]$sample['json']) $UserProfilePath 'guard-user')
        if (($sample.rule -and $sample.rule -notin $findings.rule) -or (!$sample.rule -and $findings.Count)) {
            Write-Finding '<self-test>' "sample:$checks" 'misclassified'
            $failures++
        }
    }
    $finding = @(Get-ContentFinding '{"items":[{"mask":"\u0051:\u005cwork\u005cmask.csv"}]}' $true '' '')[0]
    $exception = @{ file = 'tests/sample.json'; field = '$[''items''][0][''mask'']'; rule = 'drive-root'; reason = 'Synthetic test'; owner = 'test'; expiry = '2099-01-01' }
    $today = [datetime]'2026-01-01'
    $checks++
    if (!(Test-ExceptionValid $exception $today) -or !(Test-Excepted 'tests/sample.json' $finding @($exception))) { $failures++ }
    foreach ($change in @(@{ file = 'tests/other.json' }, @{ field = '$[''other'']' }, @{ rule = 'unc' }, @{ fingerprint = ('0' * 64) })) {
        $checks++
        $different = $exception.Clone()
        foreach ($key in $change.Keys) { $different[$key] = $change[$key] }
        if (Test-Excepted 'tests/sample.json' $finding @($different)) { $failures++ }
    }
    $exception.Remove('field')
    $exception.fingerprint = Get-Fingerprint $drive
    $checks++
    if (!(Test-Excepted 'tests/sample.json' $finding @($exception))) { $failures++ }
    foreach ($change in @(@{ expiry = '2025-12-31' }, @{ expiry = 'bad-date' }, @{ reason = '' }, @{ owner = '' }, @{ fingerprint = '' })) {
        $checks++
        $invalid = $exception.Clone()
        foreach ($key in $change.Keys) { $invalid[$key] = $change[$key] }
        if (Test-ExceptionValid $invalid $today) { $failures++ }
    }
    foreach ($file in @('tests/sample.cs', 'scripts/tests/check.ps1', 'example/panel/project.json', 'example/panel/mask.csv', 'example/panel/golden.c', 'settings.config', 'tests/golden/expected.txt')) {
        $checks++
        if (!(Test-ScanFile $file)) { $failures++ }
    }
    foreach ($file in @('docs/sample.json', 'README.md', 'example/panel/golden.bin', 'src/App.cs')) {
        $checks++
        if (Test-ScanFile $file) { $failures++ }
    }
    $privateKey = @(Get-ContentFinding ('{"' + $drive.Replace('\', '\\') + '":"relative.csv"}') $true '' '')[0]
    $checks++
    if ($privateKey.location -cne '$.*') { $failures++ }
    Write-Host "Path guard self-test: $($checks - $failures)/$checks passed."
    if ($failures) { exit 1 }
    exit 0
}

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$configFile = '.github/path-guard.json'
try {
    $config = Get-Content -LiteralPath (Join-Path $root $configFile) -Raw | ConvertFrom-Json -AsHashtable
    if ($config -isnot [System.Collections.IDictionary] -or $config['exceptions'] -isnot [array]) { throw 'Invalid config' }
    $exceptions = @($config.exceptions)
    foreach ($exception in $exceptions) {
        if (!(Test-ExceptionValid $exception ([datetime]::UtcNow.Date))) { throw 'Invalid exception' }
    }
}
catch {
    Write-Finding $configFile '$' 'invalid-exception-config'
    exit 1
}

$repositories = @(@{ root = $root; prefix = '' })
$example = Join-Path $root 'example'
if (Test-Path -LiteralPath (Join-Path $example '.git')) {
    $repositories += @{ root = $example; prefix = 'example/' }
}
else { Write-Host 'Path guard: example submodule unavailable; coverage excludes example/.' }

$count = 0
$scanned = 0
foreach ($repository in $repositories) {
    try {
        $tracked = (& git -C $repository.root ls-files --cached -z 2>$null) -join "`n"
        if ($LASTEXITCODE -ne 0) { throw 'Git listing failed' }
    }
    catch {
        Write-Finding "$($repository.prefix)." 'line:1' 'tracked-files-unavailable'
        $count++
        continue
    }
    foreach ($relative in $tracked.Split([char]0, [StringSplitOptions]::RemoveEmptyEntries)) {
        $file = $repository.prefix + $relative
        if (!(Test-ScanFile $file)) { continue }
        try {
            $fullPath = Join-Path $repository.root $relative
            # Do not follow tracked symbolic links into machine-local or untracked data.
            if ((Get-Item -LiteralPath $fullPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked file' }
            # ReadAllText decodes UTF BOMs before the binary check, including UTF-16 text goldens.
            $text = [IO.File]::ReadAllText($fullPath)
            if ($text.Contains([char]0) -and [IO.Path]::GetExtension($file) -ine '.json') { continue }
            $scanned++
            foreach ($finding in @(Get-ContentFinding $text ($file.EndsWith('.json', [StringComparison]::OrdinalIgnoreCase)) $env:USERPROFILE $env:USERNAME)) {
                if (!(Test-Excepted $file $finding $exceptions)) {
                    Write-Finding $file $finding.location $finding.rule
                    $count++
                }
            }
        }
        catch {
            Write-Finding $file 'line:1' 'unreadable-file'
            $count++
        }
    }
}
Write-Host "Path guard: $count finding(s), $scanned tracked text file(s) scanned."
if ($count) { exit 1 }
exit 0
