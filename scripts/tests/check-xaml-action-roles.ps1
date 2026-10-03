param(
  [switch]$NoThrow,
  [string]$ResultOutputPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.Xml.Linq

function Write-ResultFile {
  param(
    [hashtable]$Result
  )

  if ([string]::IsNullOrWhiteSpace($ResultOutputPath)) {
    return
  }

  $dir = Split-Path -Parent $ResultOutputPath
  if (-not [string]::IsNullOrWhiteSpace($dir) -and -not (Test-Path -LiteralPath $dir)) {
    New-Item -Path $dir -ItemType Directory -Force | Out-Null
  }

  $Result |
    ConvertTo-Json -Depth 8 |
    Set-Content -LiteralPath $ResultOutputPath -Encoding utf8
}

function Get-RepoRelativePath {
  param(
    [Parameter(Mandatory = $true)]
    [string]$Path
  )

  return [System.IO.Path]::GetRelativePath($repoRoot, $Path).Replace("\", "/")
}

function Get-XamlDocument {
  param(
    [Parameter(Mandatory = $true)]
    [string]$Path
  )

  $settings = [System.Xml.XmlReaderSettings]::new()
  $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
  $reader = [System.Xml.XmlReader]::Create($Path, $settings)
  try {
    return [System.Xml.Linq.XDocument]::Load($reader, [System.Xml.Linq.LoadOptions]::SetLineInfo)
  } finally {
    $reader.Dispose()
  }
}

function Get-AttributeValue {
  param(
    [Parameter(Mandatory = $true)]
    [System.Xml.Linq.XElement]$Element,
    [Parameter(Mandatory = $true)]
    [string]$Name
  )

  foreach ($attribute in $Element.Attributes()) {
    if ($attribute.Name.LocalName -eq $Name) {
      return $attribute.Value
    }
  }

  return $null
}

function Test-HasAnyAttribute {
  param(
    [Parameter(Mandatory = $true)]
    [System.Xml.Linq.XElement]$Element,
    [Parameter(Mandatory = $true)]
    [string[]]$Names
  )

  foreach ($attribute in $Element.Attributes()) {
    if ($Names -contains $attribute.Name.LocalName) {
      return $true
    }
  }

  return $false
}

function Get-ClassTokens {
  param(
    [Parameter(Mandatory = $true)]
    [System.Xml.Linq.XElement]$Element
  )

  $classes = Get-AttributeValue -Element $Element -Name "Classes"
  if ([string]::IsNullOrWhiteSpace($classes)) {
    return @()
  }

  return @($classes -split "\s+" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Get-LineNumber {
  param(
    [Parameter(Mandatory = $true)]
    [System.Xml.Linq.XElement]$Element
  )

  $lineInfo = [System.Xml.IXmlLineInfo]$Element
  if ($lineInfo.HasLineInfo()) {
    return $lineInfo.LineNumber
  }

  return 0
}

function Add-Issue {
  param(
    [Parameter(Mandatory = $true)]
    [string]$File,
    [int]$Line = 0,
    [Parameter(Mandatory = $true)]
    [string]$Rule,
    [Parameter(Mandatory = $true)]
    [string]$Message,
    [string]$Classes = ""
  )

  [void]$script:issues.Add([ordered]@{
      file = $File
      line = $Line
      rule = $Rule
      message = $Message
      classes = $Classes
    })
}

function Find-StyleBySelectorParts {
  param(
    [Parameter(Mandatory = $true)]
    [System.Xml.Linq.XDocument]$Document,
    [Parameter(Mandatory = $true)]
    [string[]]$SelectorParts
  )

  foreach ($style in $Document.Descendants() | Where-Object { $_.Name.LocalName -eq "Style" }) {
    $selector = Get-AttributeValue -Element $style -Name "Selector"
    if ([string]::IsNullOrWhiteSpace($selector)) {
      continue
    }

    $matchesAll = $true
    foreach ($part in $SelectorParts) {
      if (-not $selector.Contains($part)) {
        $matchesAll = $false
        break
      }
    }

    if ($matchesAll) {
      return $style
    }
  }

  return $null
}

function Get-StyleSetterProperties {
  param(
    [Parameter(Mandatory = $true)]
    [System.Xml.Linq.XElement]$Style
  )

  $properties = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
  foreach ($setter in $Style.Elements() | Where-Object { $_.Name.LocalName -eq "Setter" }) {
    $property = Get-AttributeValue -Element $setter -Name "Property"
    if (-not [string]::IsNullOrWhiteSpace($property)) {
      [void]$properties.Add($property)
    }
  }

  return $properties
}

function Assert-StyleContract {
  param(
    [Parameter(Mandatory = $true)]
    [System.Xml.Linq.XDocument]$Document,
    [Parameter(Mandatory = $true)]
    [string]$File,
    [Parameter(Mandatory = $true)]
    [string[]]$SelectorParts,
    [Parameter(Mandatory = $true)]
    [string[]]$RequiredProperties,
    [Parameter(Mandatory = $true)]
    [string]$Rule,
    [Parameter(Mandatory = $true)]
    [string]$Description
  )

  $style = Find-StyleBySelectorParts -Document $Document -SelectorParts $SelectorParts
  if ($null -eq $style) {
    Add-Issue -File $File -Rule $Rule -Message "$Description style selector is missing."
    return
  }

  $properties = Get-StyleSetterProperties -Style $style
  $missing = @($RequiredProperties | Where-Object { -not $properties.Contains($_) })
  if ($missing.Count -gt 0) {
    Add-Issue `
      -File $File `
      -Line (Get-LineNumber -Element $style) `
      -Rule $Rule `
      -Message "$Description style selector is missing setter(s): $($missing -join ', ')." `
      -Classes (Get-AttributeValue -Element $style -Name "Selector")
  }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$issues = [System.Collections.Generic.List[object]]::new()

$roleResult = [ordered]@{
  generatedUtc = [DateTime]::UtcNow.ToString("o")
  scannedFiles = 0
  checkedElements = 0
  issueCount = 0
  issues = @()
  overallStatus = "pending"
  failureMessage = ""
}

$legacyTokens = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
@(
  "panelAction",
  "panelToggle",
  "coordinateOverlayAction",
  "workspacePrimaryAction",
  "workspaceDangerAction",
  "workspacePresetButton",
  "workspaceSummaryChipButton",
  "workspaceSummaryChip"
) | ForEach-Object {
  [void]$legacyTokens.Add($_)
}

$interactiveAttributeNames = @(
  "Command",
  "Button.Command",
  "ToggleButton.Command",
  "Click",
  "Tapped",
  "DoubleTapped",
  "PointerPressed",
  "PointerReleased"
)

$scanDirectories = @(
  "src/FreeformHelper.UI/Views",
  "src/FreeformHelper.UI/Controls"
)

Push-Location $repoRoot
try {
  $xamlFiles = foreach ($dir in $scanDirectories) {
    if (Test-Path -LiteralPath $dir) {
      Get-ChildItem -Path $dir -Recurse -Filter *.axaml -File
    }
  }

  foreach ($file in @($xamlFiles | Sort-Object FullName)) {
    $relativePath = Get-RepoRelativePath -Path $file.FullName
    $roleResult.scannedFiles++
    $document = Get-XamlDocument -Path $file.FullName
    $isDevPreview = $relativePath -eq "src/FreeformHelper.UI/Views/DevView.axaml" -or
      $relativePath.StartsWith("src/FreeformHelper.UI/Views/DevSections/", [System.StringComparison]::Ordinal)

    foreach ($element in $document.Descendants()) {
      $tokens = @(Get-ClassTokens -Element $element)
      if ($tokens.Count -eq 0) {
        continue
      }

      $roleResult.checkedElements++
      $tokenSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
      foreach ($token in $tokens) {
        [void]$tokenSet.Add($token)
      }
      $line = Get-LineNumber -Element $element
      $classes = $tokens -join " "
      $elementName = $element.Name.LocalName

      foreach ($token in $tokens) {
        if ($legacyTokens.Contains($token)) {
          Add-Issue `
            -File $relativePath `
            -Line $line `
            -Rule "XAR001" `
            -Message "Legacy action role token '$token' is not allowed in active XAML; migrate to action primitive + semantic role." `
            -Classes $classes
        }
      }

      if ($tokenSet.Contains("consoleHeaderAction") -and $tokenSet.Contains("icon")) {
        Add-Issue -File $relativePath -Line $line -Rule "XAR002" -Message "consoleHeaderAction must not be combined with legacy icon." -Classes $classes
      }

      if ($tokenSet.Contains("viewportOverlayAction") -and $tokenSet.Contains("panelAction")) {
        Add-Issue -File $relativePath -Line $line -Rule "XAR003" -Message "viewportOverlayAction must not be combined with panelAction." -Classes $classes
      }

      if ($tokenSet.Contains("viewportOverlayAction") -and $tokenSet.Contains("panelChromeToggle")) {
        Add-Issue -File $relativePath -Line $line -Rule "XAR004" -Message "viewportOverlayAction must not be combined with panelChromeToggle." -Classes $classes
      }

      $primitiveCount = @($tokens | Where-Object { $_ -in @("actionButton", "actionTextButton", "actionIconButton", "actionChip") }).Count
      if ($primitiveCount -gt 1) {
        Add-Issue -File $relativePath -Line $line -Rule "XAR005" -Message "Action controls must use one primitive class only." -Classes $classes
      }

      if ($tokenSet.Contains("actionIconButton")) {
        if ($elementName -ne "Button" -and $elementName -ne "ToggleButton") {
          Add-Issue -File $relativePath -Line $line -Rule "XAR006" -Message "actionIconButton is only valid on Button or ToggleButton." -Classes $classes
        }
      }

      foreach ($iconOnlyRole in @("consoleHeaderAction", "viewportOverlayAction", "panelChromeToggle")) {
        if ($tokenSet.Contains($iconOnlyRole) -and -not $tokenSet.Contains("actionIconButton")) {
          Add-Issue -File $relativePath -Line $line -Rule "XAR007" -Message "$iconOnlyRole must use actionIconButton so fixed icon sizing/clip contract applies." -Classes $classes
        }
      }

      if ($tokenSet.Contains("chipStatus")) {
        if ($elementName -ne "Border") {
          Add-Issue -File $relativePath -Line $line -Rule "XAR008" -Message "chipStatus is passive status UI and must be rendered on Border." -Classes $classes
        }

        if (Test-HasAnyAttribute -Element $element -Names $interactiveAttributeNames) {
          Add-Issue -File $relativePath -Line $line -Rule "XAR009" -Message "chipStatus must not bind Command or click/tap handlers." -Classes $classes
        }

        if ($tokenSet.Contains("chipAction") -or $tokenSet.Contains("actionChip")) {
          Add-Issue -File $relativePath -Line $line -Rule "XAR010" -Message "chipStatus must not be mixed with action chip classes." -Classes $classes
        }
      }

      if ($tokenSet.Contains("chipAction")) {
        if ($elementName -ne "Button" -and $elementName -ne "ToggleButton") {
          Add-Issue -File $relativePath -Line $line -Rule "XAR011" -Message "chipAction must be rendered on Button or ToggleButton." -Classes $classes
        }

        if (-not $tokenSet.Contains("actionChip")) {
          Add-Issue -File $relativePath -Line $line -Rule "XAR012" -Message "chipAction must include actionChip primitive." -Classes $classes
        }

        if (-not $isDevPreview -and -not (Test-HasAnyAttribute -Element $element -Names $interactiveAttributeNames)) {
          Add-Issue -File $relativePath -Line $line -Rule "XAR013" -Message "chipAction must bind Command or an explicit click/tap handler outside Dev preview samples." -Classes $classes
        }
      }
    }
  }

  $actionStylesPath = Join-Path $repoRoot "src/FreeformHelper.UI/Styles/Controls.Action.axaml"
  $actionStylesRelativePath = Get-RepoRelativePath -Path $actionStylesPath
  if (-not (Test-Path -LiteralPath $actionStylesPath)) {
    Add-Issue -File $actionStylesRelativePath -Rule "XAR100" -Message "Controls.Action.axaml is required for action role contracts."
  } else {
    $styleDocument = Get-XamlDocument -Path $actionStylesPath
    Assert-StyleContract `
      -Document $styleDocument `
      -File $actionStylesRelativePath `
      -SelectorParts @("Button.actionIconButton", "ToggleButton.actionIconButton") `
      -RequiredProperties @("Width", "MinWidth", "Height", "MinHeight", "ClipToBounds") `
      -Rule "XAR101" `
      -Description "actionIconButton"

    Assert-StyleContract `
      -Document $styleDocument `
      -File $actionStylesRelativePath `
      -SelectorParts @("Button.chipAction:pointerover", "ToggleButton.chipAction:pointerover") `
      -RequiredProperties @("Background", "BorderBrush", "Foreground") `
      -Rule "XAR102" `
      -Description "chipAction pointerover"

    Assert-StyleContract `
      -Document $styleDocument `
      -File $actionStylesRelativePath `
      -SelectorParts @("Button.chipAction:pressed", "ToggleButton.chipAction:pressed") `
      -RequiredProperties @("Background", "BorderBrush", "Foreground") `
      -Rule "XAR103" `
      -Description "chipAction pressed"
  }

  $roleResult.issueCount = $issues.Count
  $roleResult.issues = @($issues)

  if ($issues.Count -gt 0) {
    $roleResult.overallStatus = "failed"
    foreach ($issue in $issues) {
      $location = if ($issue.line -gt 0) { "$($issue.file):$($issue.line)" } else { $issue.file }
      $classText = if ([string]::IsNullOrWhiteSpace($issue.classes)) { "" } else { " Classes=`"$($issue.classes)`"" }
      Write-Host "[xaml-action-roles] ERROR $location $($issue.rule): $($issue.message)$classText"
    }

    if (-not $NoThrow) {
      throw "[xaml-action-roles] failed with $($issues.Count) issue(s)."
    }
  } else {
    $roleResult.overallStatus = "ok"
    Write-Host ("[xaml-action-roles] scanned={0}, checkedElements={1}, issues=0" -f $roleResult.scannedFiles, $roleResult.checkedElements)
  }
}
catch {
  if ($roleResult.overallStatus -ne "failed") {
    $roleResult.overallStatus = "failed"
    $roleResult.failureMessage = $_.Exception.Message
  }

  if (-not $NoThrow) {
    Write-ResultFile -Result $roleResult
    throw
  }
}
finally {
  $roleResult.issueCount = $issues.Count
  $roleResult.issues = @($issues)
  Write-ResultFile -Result $roleResult
  Pop-Location
}
