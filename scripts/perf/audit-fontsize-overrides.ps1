param(
    [string]$Root = "src/FreeformHelper.UI",
    [string]$OutPath = "build/perf/fontsize-overrides-latest.md"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\\..")
$scanRoot = if ([System.IO.Path]::IsPathRooted($Root)) {
    [System.IO.Path]::GetFullPath($Root)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Root))
}

if (-not (Test-Path -LiteralPath $scanRoot)) {
    throw "Scan root not found: $scanRoot"
}

$regex = [System.Text.RegularExpressions.Regex]::new(
    'FontSize\s*=\s*"(?<value>[^"]+)"',
    [System.Text.RegularExpressions.RegexOptions]::Compiled)

function Get-RelativePathCompat([string]$basePath, [string]$targetPath) {
    $pathType = [System.IO.Path]
    $method = $pathType.GetMethod("GetRelativePath", [Type[]]@([string], [string]))
    if ($null -ne $method) {
        return [System.IO.Path]::GetRelativePath($basePath, $targetPath).Replace('\', '/')
    }

    $baseFull = [System.IO.Path]::GetFullPath($basePath)
    $targetFull = [System.IO.Path]::GetFullPath($targetPath)
    if (-not $baseFull.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $baseFull += [System.IO.Path]::DirectorySeparatorChar
    }

    $baseUri = [System.Uri]::new($baseFull)
    $targetUri = [System.Uri]::new($targetFull)
    $relative = $baseUri.MakeRelativeUri($targetUri).ToString()
    return [System.Uri]::UnescapeDataString($relative).Replace('\', '/')
}

function Get-Category([string]$value) {
    if ($value -match 'GlobalFontBaseSize') { return "global-scale-binding" }
    if ($value -match '^\{Binding') { return "binding" }
    if ($value -match '^\{(DynamicResource|StaticResource)\s+FontSize') { return "font-token" }
    if ($value -match '^\d+(\.\d+)?$') { return "literal" }
    return "other"
}

$hits = New-Object System.Collections.Generic.List[object]
$files = Get-ChildItem -LiteralPath $scanRoot -Recurse -File |
    Where-Object {
        $_.Extension -ieq ".axaml" -and
        $_.FullName -notmatch '[\\/](bin|obj|build|tmp_obj)[\\/]'
    }
foreach ($file in $files) {
    $lineNo = 0
    Get-Content -LiteralPath $file.FullName | ForEach-Object {
        $lineNo++
        $line = $_
        $m = $regex.Match($line)
        if (-not $m.Success) {
            return
        }

        $value = $m.Groups["value"].Value
        $category = Get-Category $value
        $relativePath = Get-RelativePathCompat -basePath $repoRoot -targetPath $file.FullName
        $hits.Add([PSCustomObject]@{
                file = $relativePath
                line = $lineNo
                value = $value
                category = $category
            }) | Out-Null
    }
}

$grouped = @($hits | Group-Object category | Sort-Object Name)
$markdown = New-Object System.Collections.Generic.List[string]
$markdown.Add("# FontSize Override Audit")
$markdown.Add(("Generated: {0}" -f ([DateTime]::UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"))))
$markdown.Add(("Scan root: {0}" -f $scanRoot))
$markdown.Add(("Total hits: {0}" -f $hits.Count))
$markdown.Add("")
$markdown.Add("## Category Summary")
$markdown.Add("| Category | Count |")
$markdown.Add("| --- | ---: |")
foreach ($g in $grouped) {
    $markdown.Add(("| {0} | {1} |" -f $g.Name, $g.Count))
}

$markdown.Add("")
$markdown.Add("## Hits")
$markdown.Add("| Category | File | Line | Value |")
$markdown.Add("| --- | --- | ---: | --- |")
foreach ($h in $hits) {
    $safeValue = $h.value -replace '\|', '/'
    $markdown.Add(("| {0} | `{1}` | {2} | `{3}` |" -f $h.category, $h.file, $h.line, $safeValue))
}

$outFullPath = if ([System.IO.Path]::IsPathRooted($OutPath)) {
    [System.IO.Path]::GetFullPath($OutPath)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutPath))
}

$outDir = Split-Path -Parent $outFullPath
if (-not [string]::IsNullOrWhiteSpace($outDir) -and -not (Test-Path -LiteralPath $outDir)) {
    New-Item -Path $outDir -ItemType Directory -Force | Out-Null
}

Set-Content -LiteralPath $outFullPath -Value $markdown -Encoding utf8
Write-Host "FontSize audit generated: $outFullPath"
