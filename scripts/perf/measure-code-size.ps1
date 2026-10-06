param(
  [string]$OutJsonPath = "build/code-size/code-size-baseline.json",
  [string]$ReferenceCommit = "3032121156dec2329e38e971327d55c7887db99f",
  [switch]$SkipReleaseBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function New-Metric {
  return [PSCustomObject][ordered]@{
    files = 0
    physical = 0
    nonblank = 0
  }
}

function Add-ToMetric {
  param(
    [object]$Metric,
    [object]$Value
  )

  $Metric.files += [int]$Value.files
  $Metric.physical += [int]$Value.physical
  $Metric.nonblank += [int]$Value.nonblank
}

function Add-Metrics {
  param(
    [object[]]$Metrics
  )

  $result = New-Metric
  foreach ($metric in $Metrics) {
    Add-ToMetric -Metric $result -Value $metric
  }

  return $result
}

function Get-LineMetricFromText {
  param(
    [AllowEmptyString()]
    [string]$Text
  )

  if ($Text.Length -eq 0) {
    return [PSCustomObject][ordered]@{
      files = 1
      physical = 0
      nonblank = 0
    }
  }

  $lines = [System.Text.RegularExpressions.Regex]::Split($Text, "\r\n|\n|\r")
  $physical = $lines.Count
  if ($Text.EndsWith("`n", [StringComparison]::Ordinal) -or
      $Text.EndsWith("`r", [StringComparison]::Ordinal)) {
    $physical--
  }

  $nonblank = 0
  for ($index = 0; $index -lt $physical; $index++) {
    if (-not [string]::IsNullOrWhiteSpace($lines[$index])) {
      $nonblank++
    }
  }

  return [PSCustomObject][ordered]@{
    files = 1
    physical = $physical
    nonblank = $nonblank
  }
}

function Assert-LineCounterContract {
  $cases = @(
    @{ text = ""; physical = 0; nonblank = 0 },
    @{ text = "a"; physical = 1; nonblank = 1 },
    @{ text = "a`r`n"; physical = 1; nonblank = 1 },
    @{ text = "a`n`n"; physical = 2; nonblank = 1 },
    @{ text = " `r"; physical = 1; nonblank = 0 },
    @{ text = "a`rb"; physical = 2; nonblank = 2 }
  )

  foreach ($case in $cases) {
    $metric = Get-LineMetricFromText -Text $case.text
    if ($metric.files -ne 1 -or
        $metric.physical -ne $case.physical -or
        $metric.nonblank -ne $case.nonblank) {
      throw "Internal line counter contract failed."
    }
  }

}

function Test-IsExcludedProductionPath {
  param(
    [string]$Path
  )

  $normalized = "/" + $Path.Replace("\", "/").TrimStart("/")
  foreach ($segment in @(
      "/assets/",
      "/bin/",
      "/build/",
      "/docs/",
      "/generated/",
      "/golden/",
      "/goldens/",
      "/obj/",
      "/scripts/",
      "/test/",
      "/tests/")) {
    if ($normalized.IndexOf($segment, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
      return $true
    }
  }

  $fileName = [System.IO.Path]::GetFileName($normalized)
  foreach ($suffix in @(".g.cs", ".g.i.cs", ".generated.cs", ".g.axaml", ".generated.axaml")) {
    if ($fileName.EndsWith($suffix, [StringComparison]::OrdinalIgnoreCase)) {
      return $true
    }
  }

  return $false
}

function Get-SourceGroupName {
  param(
    [string]$Path
  )

  if ($Path.StartsWith("src/FreeformHelper.Domain/", [StringComparison]::Ordinal) -and
      $Path.EndsWith(".cs", [StringComparison]::OrdinalIgnoreCase)) {
    return "domain"
  }
  if ($Path.StartsWith("src/FreeformHelper.Application/", [StringComparison]::Ordinal) -and
      $Path.EndsWith(".cs", [StringComparison]::OrdinalIgnoreCase)) {
    return "application"
  }
  if ($Path.StartsWith("src/FreeformHelper.Infrastructure/", [StringComparison]::Ordinal) -and
      $Path.EndsWith(".cs", [StringComparison]::OrdinalIgnoreCase)) {
    return "infrastructure"
  }
  if ($Path.StartsWith("src/FreeformHelper.UI/", [StringComparison]::Ordinal)) {
    if ($Path.EndsWith(".cs", [StringComparison]::OrdinalIgnoreCase)) {
      return "uiCSharp"
    }
    if ($Path.EndsWith(".axaml", [StringComparison]::OrdinalIgnoreCase)) {
      return "uiAxaml"
    }
  }

  return $null
}

function Get-UiSubsetName {
  param(
    [string]$Path
  )

  if (-not $Path.EndsWith(".cs", [StringComparison]::OrdinalIgnoreCase)) {
    return $null
  }
  if ($Path.StartsWith("src/FreeformHelper.UI/ViewModels/", [StringComparison]::Ordinal)) {
    return "uiViewModels"
  }
  if ($Path.StartsWith("src/FreeformHelper.UI/Services/", [StringComparison]::Ordinal)) {
    return "uiServices"
  }

  return $null
}

function Assert-PathClassifierContract {
  $cases = @(
    @{ path = "src/FreeformHelper.Domain/Model.cs"; excluded = $false; group = "domain"; subset = $null },
    @{ path = "src/FreeformHelper.Application/Service.cs"; excluded = $false; group = "application"; subset = $null },
    @{ path = "src/FreeformHelper.Infrastructure/Store.cs"; excluded = $false; group = "infrastructure"; subset = $null },
    @{ path = "src/FreeformHelper.UI/ViewModels/MainViewModel.cs"; excluded = $false; group = "uiCSharp"; subset = "uiViewModels" },
    @{ path = "src/FreeformHelper.UI/Services/WorkflowService.cs"; excluded = $false; group = "uiCSharp"; subset = "uiServices" },
    @{ path = "src/FreeformHelper.UI/Views/MainView.axaml"; excluded = $false; group = "uiAxaml"; subset = $null },
    @{ path = "src/FreeformHelper.UI/Assets/Fake.cs"; excluded = $true; group = "uiCSharp"; subset = $null },
    @{ path = "src/FreeformHelper.UI/Generated/Fake.cs"; excluded = $true; group = "uiCSharp"; subset = $null },
    @{ path = "src/FreeformHelper.Application/obj/Fake.cs"; excluded = $true; group = "application"; subset = $null },
    @{ path = "src/FreeformHelper.Application/Fake.g.cs"; excluded = $true; group = "application"; subset = $null }
  )

  foreach ($case in $cases) {
    $excluded = Test-IsExcludedProductionPath -Path $case.path
    $group = Get-SourceGroupName -Path $case.path
    $subset = Get-UiSubsetName -Path $case.path
    if ($excluded -ne $case.excluded -or $group -ne $case.group -or $subset -ne $case.subset) {
      throw "Internal source path classifier contract failed for '$($case.path)'."
    }
  }

}

function Get-SourceMetrics {
  param(
    [string]$SourceRoot,
    [string[]]$TrackedPaths
  )

  $groups = [ordered]@{
    domain = New-Metric
    application = New-Metric
    infrastructure = New-Metric
    uiCSharp = New-Metric
    uiAxaml = New-Metric
  }
  $subsets = [ordered]@{
    uiViewModels = New-Metric
    uiServices = New-Metric
  }

  foreach ($path in $TrackedPaths) {
    $normalizedPath = $path.Replace("\", "/")
    if (-not ($normalizedPath.EndsWith(".cs", [StringComparison]::OrdinalIgnoreCase) -or
        $normalizedPath.EndsWith(".axaml", [StringComparison]::OrdinalIgnoreCase))) {
      continue
    }
    if (Test-IsExcludedProductionPath -Path $normalizedPath) {
      continue
    }

    $groupName = Get-SourceGroupName -Path $normalizedPath
    if ([string]::IsNullOrWhiteSpace($groupName)) {
      throw "Tracked production source is outside the documented groups: $normalizedPath"
    }

    $fullPath = Join-Path $SourceRoot $normalizedPath.Replace("/", "\")
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
      throw "Tracked production source is missing: $fullPath"
    }

    $metric = Get-LineMetricFromText -Text ([System.IO.File]::ReadAllText($fullPath))
    Add-ToMetric -Metric $groups[$groupName] -Value $metric

    $subsetName = Get-UiSubsetName -Path $normalizedPath
    if (-not [string]::IsNullOrWhiteSpace($subsetName)) {
      Add-ToMetric -Metric $subsets[$subsetName] -Value $metric
    }
  }

  $total = Add-Metrics @(
    $groups.domain,
    $groups.application,
    $groups.infrastructure,
    $groups.uiCSharp,
    $groups.uiAxaml)
  $logicFirst = Add-Metrics @(
    $groups.domain,
    $groups.application,
    $groups.infrastructure,
    $subsets.uiViewModels,
    $subsets.uiServices)

  return [PSCustomObject][ordered]@{
    total = $total
    logicFirst = $logicFirst
    groups = [PSCustomObject]$groups
    subsets = [PSCustomObject]$subsets
  }
}

function New-DeltaMetric {
  param(
    [object]$Current,
    [object]$Reference
  )

  return [PSCustomObject][ordered]@{
    files = [int]$Current.files - [int]$Reference.files
    physical = [int]$Current.physical - [int]$Reference.physical
    nonblank = [int]$Current.nonblank - [int]$Reference.nonblank
  }
}

function New-SourceDelta {
  param(
    [object]$Current,
    [object]$Reference
  )

  $groupDelta = [ordered]@{}
  foreach ($name in @("domain", "application", "infrastructure", "uiCSharp", "uiAxaml")) {
    $groupDelta[$name] = New-DeltaMetric -Current $Current.groups.$name -Reference $Reference.groups.$name
  }
  $subsetDelta = [ordered]@{}
  foreach ($name in @("uiViewModels", "uiServices")) {
    $subsetDelta[$name] = New-DeltaMetric -Current $Current.subsets.$name -Reference $Reference.subsets.$name
  }

  return [PSCustomObject][ordered]@{
    total = New-DeltaMetric -Current $Current.total -Reference $Reference.total
    logicFirst = New-DeltaMetric -Current $Current.logicFirst -Reference $Reference.logicFirst
    groups = [PSCustomObject]$groupDelta
    subsets = [PSCustomObject]$subsetDelta
  }
}

function Remove-SafeTempDirectory {
  param(
    [string]$Path,
    [string]$AllowedRoot
  )

  if (-not (Test-Path -LiteralPath $Path)) {
    return
  }

  $target = [System.IO.Path]::GetFullPath($Path)
  $root = [System.IO.Path]::GetFullPath($AllowedRoot).TrimEnd("\", "/") + [System.IO.Path]::DirectorySeparatorChar
  if (-not $target.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to remove temp path outside '$AllowedRoot': $target"
  }

  Remove-Item -LiteralPath $target -Recurse -Force
}

function Get-Artifact {
  param(
    [string]$ArtifactsRoot,
    [string]$ProjectName,
    [string]$RepoRoot
  )

  $path = Join-Path $ArtifactsRoot "bin/$ProjectName/release/$ProjectName.dll"
  if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
    throw "Release artifact not found: $path"
  }

  return [PSCustomObject][ordered]@{
    name = "$ProjectName.dll"
    path = [System.IO.Path]::GetRelativePath($RepoRoot, $path).Replace("\", "/")
    bytes = (Get-Item -LiteralPath $path).Length
    sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
  }
}

function Invoke-ReleaseBuild {
  param(
    [string]$Label,
    [string]$ArtifactsRoot,
    [string]$RepoRoot
  )

  $uiProject = "src/FreeformHelper.UI/FreeformHelper.UI.csproj"
  $pathMap = "$ArtifactsRoot=/_/artifacts%2C$RepoRoot=/_/repo"
  $arguments = @(
    "build",
    $uiProject,
    "-c", "Release",
    "--nologo",
    "--no-incremental",
    "--artifacts-path", $ArtifactsRoot,
    "-p:UseAppHost=false",
    "-p:ContinuousIntegrationBuild=true",
    "-p:Deterministic=true",
    "-p:IncludeSourceRevisionInInformationalVersion=false",
    "-p:DebugType=None",
    "-p:DebugSymbols=false",
    "-p:PathMap=$pathMap"
  )
  Write-Host "[code-size] Release build $Label..."
  & dotnet @arguments | ForEach-Object { Write-Host $_ }
  if ($LASTEXITCODE -ne 0) {
    throw "Release build '$Label' failed with exit code $LASTEXITCODE."
  }

  $artifacts = foreach ($projectName in @(
      "FreeformHelper.Domain",
      "FreeformHelper.Application",
      "FreeformHelper.Infrastructure",
      "FreeformHelper.UI")) {
    Get-Artifact -ArtifactsRoot $ArtifactsRoot -ProjectName $projectName -RepoRoot $RepoRoot
  }

  return [PSCustomObject][ordered]@{
    label = $Label
    artifactsRoot = [System.IO.Path]::GetRelativePath($RepoRoot, $ArtifactsRoot).Replace("\", "/")
    artifacts = @($artifacts)
    totalBytes = [long](@($artifacts | Measure-Object -Property bytes -Sum).Sum)
  }
}

function Get-ReleaseArtifactResult {
  param(
    [string]$RepoRoot,
    [string]$SessionId,
    [bool]$BuildInputsDirty,
    [switch]$Skip
  )

  if ($Skip) {
    return [PSCustomObject][ordered]@{
      secondary = $true
      skipped = $true
      reason = "SkipReleaseBuild was requested."
      cleanBuildInputs = -not $BuildInputsDirty
      authoritative = $false
      reproducible = $null
      configuration = "Release"
      builds = @()
      artifacts = @()
      totalBytes = $null
    }
  }

  if ($BuildInputsDirty) {
    throw "Release artifact measurement requires clean tracked build inputs. Commit or revert src/build configuration changes."
  }

  $releaseRoot = Join-Path $RepoRoot "build/code-size/artifacts/$SessionId"
  $first = Invoke-ReleaseBuild -Label "first" -ArtifactsRoot (Join-Path $releaseRoot "first") -RepoRoot $RepoRoot
  $second = Invoke-ReleaseBuild -Label "second" -ArtifactsRoot (Join-Path $releaseRoot "second") -RepoRoot $RepoRoot

  $summary = @()
  for ($index = 0; $index -lt $first.artifacts.Count; $index++) {
    $left = $first.artifacts[$index]
    $right = $second.artifacts[$index]
    $reproducible = $left.name -eq $right.name -and $left.bytes -eq $right.bytes -and $left.sha256 -eq $right.sha256
    $summary += [PSCustomObject][ordered]@{
      name = $left.name
      bytes = $left.bytes
      sha256 = $left.sha256
      reproducible = $reproducible
    }
  }

  $allReproducible = @($summary | Where-Object { -not $_.reproducible }).Count -eq 0
  if (-not $allReproducible) {
    throw "Release assembly hashes or sizes differ between clean isolated builds."
  }

  return [PSCustomObject][ordered]@{
    secondary = $true
    skipped = $false
    reason = ""
    cleanBuildInputs = -not $BuildInputsDirty
    authoritative = (-not $BuildInputsDirty) -and $allReproducible
    reproducible = $true
    configuration = "Release"
    targetFramework = "net10.0"
    debugPolicy = "No PDB: DebugType=None and DebugSymbols=false."
    isolationPolicy = "Each build uses a new ArtifactsPath containing its own bin and obj trees; the immutable global NuGet package cache may be shared."
    reproducibilityPolicy = "Exact bytes and SHA-256 for the four primary project DLLs under the recorded checkout, SDK, OS, architecture, RID, TFM, and flags."
    command = "dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Release --nologo --no-incremental --artifacts-path <isolated> -p:UseAppHost=false -p:ContinuousIntegrationBuild=true -p:Deterministic=true -p:IncludeSourceRevisionInInformationalVersion=false -p:DebugType=None -p:DebugSymbols=false -p:PathMap=<isolated>=/_/artifacts%2C<repo>=/_/repo"
    builds = @($first, $second)
    artifacts = @($summary)
    totalBytes = $first.totalBytes
  }
}

Assert-LineCounterContract
Assert-PathClassifierContract

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$sessionId = "{0}-{1}" -f ([DateTime]::UtcNow.ToString("yyyyMMddTHHmmssfffZ")), $PID
$tempRoot = Join-Path $repoRoot "build/code-size/temp/$sessionId"
$tempAllowedRoot = Join-Path $repoRoot "build/code-size/temp"
$referenceArchive = Join-Path $tempRoot "reference.zip"
$referenceRoot = Join-Path $tempRoot "reference"
$manifest = $null

Push-Location $repoRoot
try {
  $workspacePrepared = $false
  if (-not $SkipReleaseBuild) {
    $prepareScript = Join-Path $repoRoot "scripts/dev/prepare-ui-workspace.ps1"
    $prepareResultPath = Join-Path $repoRoot "build/test-gate/code-size-prepare-result.json"
    & $prepareScript -ResultOutputPath $prepareResultPath
    $workspacePrepared = $true
  }

  $referenceCommitFull = (git rev-parse "$ReferenceCommit^{commit}").Trim()
  if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($referenceCommitFull)) {
    throw "Reference commit cannot be resolved: $ReferenceCommit"
  }

  New-Item -Path $tempRoot -ItemType Directory -Force | Out-Null
  git archive --format=zip --output=$referenceArchive $referenceCommitFull -- src
  if ($LASTEXITCODE -ne 0) {
    throw "Failed to archive reference source at $referenceCommitFull."
  }
  Expand-Archive -LiteralPath $referenceArchive -DestinationPath $referenceRoot

  $referencePaths = @(git ls-tree -r --name-only $referenceCommitFull -- src)
  if ($LASTEXITCODE -ne 0) {
    throw "Failed to list reference source at $referenceCommitFull."
  }
  $currentTrackedPaths = @(git ls-files -- src)
  if ($LASTEXITCODE -ne 0) {
    throw "Failed to list current tracked source files."
  }
  $currentPaths = @($currentTrackedPaths | Where-Object {
      Test-Path -LiteralPath (Join-Path $repoRoot $_.Replace("/", "\")) -PathType Leaf
    })

  $referenceMetrics = Get-SourceMetrics -SourceRoot $referenceRoot -TrackedPaths $referencePaths
  $currentMetrics = Get-SourceMetrics -SourceRoot $repoRoot -TrackedPaths $currentPaths
  $currentCommit = (git rev-parse HEAD).Trim()
  $currentSourceTree = (git rev-parse "${currentCommit}:src").Trim()
  $referenceSourceTree = (git rev-parse "${referenceCommitFull}:src").Trim()
  $branch = (git branch --show-current).Trim()
  $repoStatus = @(git status --porcelain=v1 --untracked-files=all)
  $productionStatus = @(git status --porcelain=v1 --untracked-files=all -- src)
  $buildInputStatus = @(git status --porcelain=v1 --untracked-files=all -- `
      src `
      Directory.Build.props `
      Directory.Build.targets `
      Directory.Packages.props `
      Directory.Packages.targets `
      FreeformHelper.sln `
      global.json `
      NuGet.Config)
  $buildInputsDirty = $buildInputStatus.Count -gt 0
  $dotnetSdk = (dotnet --version).Trim()
  $msbuildVersion = [string](@(dotnet msbuild -version -nologo) |
      Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
      Select-Object -Last 1)
  $packageLocks = @(git ls-files -- "*packages.lock.json")

  $releaseArtifacts = Get-ReleaseArtifactResult `
    -RepoRoot $repoRoot `
    -SessionId $sessionId `
    -BuildInputsDirty $buildInputsDirty `
    -Skip:$SkipReleaseBuild

  $manifest = [ordered]@{
    schemaVersion = 1
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    contractSelfTest = "ok"
    repository = [ordered]@{
      branch = $branch
      isDirty = $repoStatus.Count -gt 0
      dotnetSdk = $dotnetSdk
      msbuildVersion = $msbuildVersion.Trim()
      osDescription = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
      osArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
      processArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
      runtimeIdentifier = [System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier
      globalJsonPresent = Test-Path -LiteralPath (Join-Path $repoRoot "global.json") -PathType Leaf
      packageLocks = @($packageLocks)
    }
    definitions = [ordered]@{
      productionIncludes = @(
        "tracked src/FreeformHelper.Domain/**/*.cs",
        "tracked src/FreeformHelper.Application/**/*.cs",
        "tracked src/FreeformHelper.Infrastructure/**/*.cs",
        "tracked src/FreeformHelper.UI/**/*.cs",
        "tracked src/FreeformHelper.UI/**/*.axaml")
      explicitExclusions = @("tests", "docs", "scripts", "generated output", "bin", "obj", "build", "assets", "goldens")
      physicalLine = "CRLF, LF, and lone CR are separators; a terminal newline does not create an extra sentinel line; an eligible empty file counts as one file and zero lines."
      nonblankLine = "A physical line counts when String.IsNullOrWhiteSpace(line) is false."
      total = "Domain + Application + Infrastructure + UI C# + UI AXAML; groups are mutually exclusive."
      logicFirst = "Domain + Application + Infrastructure + UI ViewModels C# + UI Services C#; UI subsets are not added to total twice."
      currentContent = "Current source metrics read eligible tracked paths from the working tree; baseCommit/baseSourceTree identify the index baseline and productionSourceDirty records divergence."
      artifactPolicy = "Secondary metric: four primary Release DLLs from two fresh isolated no-apphost builds; sizes and SHA-256 must match."
    }
    source = [ordered]@{
      reference = [ordered]@{
        commit = $referenceCommitFull
        sourceTree = $referenceSourceTree
        content = "commitArchive"
        metrics = $referenceMetrics
      }
      current = [ordered]@{
        content = "trackedWorkingTree"
        baseCommit = $currentCommit
        baseSourceTree = $currentSourceTree
        productionSourceDirty = $productionStatus.Count -gt 0
        metrics = $currentMetrics
      }
      deltaFromReference = New-SourceDelta -Current $currentMetrics -Reference $referenceMetrics
    }
    workspacePrepared = $workspacePrepared
    releaseArtifacts = $releaseArtifacts
    overallStatus = "ok"
  }

  $outFullPath = if ([System.IO.Path]::IsPathRooted($OutJsonPath)) {
    $OutJsonPath
  } else {
    Join-Path $repoRoot $OutJsonPath
  }
  $outDirectory = Split-Path -Parent $outFullPath
  if (-not [string]::IsNullOrWhiteSpace($outDirectory)) {
    New-Item -Path $outDirectory -ItemType Directory -Force | Out-Null
  }
  $json = $manifest | ConvertTo-Json -Depth 12
  [System.IO.File]::WriteAllText($outFullPath, $json, [System.Text.UTF8Encoding]::new($false))

  Write-Host ("[code-size] current source: files={0}, physical={1}, nonblank={2}" -f `
      $currentMetrics.total.files, $currentMetrics.total.physical, $currentMetrics.total.nonblank)
  Write-Host ("[code-size] logic-first: files={0}, physical={1}, nonblank={2}" -f `
      $currentMetrics.logicFirst.files, $currentMetrics.logicFirst.physical, $currentMetrics.logicFirst.nonblank)
  Write-Host ("[code-size] manifest: {0}" -f [System.IO.Path]::GetRelativePath($repoRoot, $outFullPath))
}
finally {
  Remove-SafeTempDirectory -Path $tempRoot -AllowedRoot $tempAllowedRoot
  Pop-Location
}
