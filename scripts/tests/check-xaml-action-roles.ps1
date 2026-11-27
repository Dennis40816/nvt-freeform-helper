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
  "actionTextButton",
  "actionChip",
  "actionButton",
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

$publicRoles = @("actionPrimary", "actionNeutral", "actionDanger", "actionGhost", "actionIconButton", "chipAction")

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
  "src/FreeformHelper.UI/Controls",
  "src/FreeformHelper.UI/Styles"
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
        if ($element.Name.LocalName -in @("Button", "ToggleButton") -and
            -not @($element.Ancestors() | Where-Object { $_.Name.LocalName -eq "ControlTemplate" }).Count) {
          Add-Issue -File $relativePath -Line (Get-LineNumber -Element $element) -Rule "XAR016" -Message "General action buttons require a Core role or an explicit product class."
        }
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

      foreach ($attribute in $element.Attributes()) {
        if ($attribute.Name.LocalName -match '^Classes\.(actionTextButton|actionChip|actionButton)$') {
          Add-Issue -File $relativePath -Line $line -Rule "XAR001" -Message "Removed role class binding is not allowed." -Classes $classes
        }
      }
      $productRoles = @("viewportOverlayAction", "canvasQuickToggle", "canvasToolbarIcon", "productChipAction", "productFilterChip", "shellTab", "rightPanelTab", "rightPanelBrowserTab", "settingsNavItem", "panelBlockHeader", "workspaceFoldHeader", "workspaceTableHeaderButton")
      if (@($tokens | Where-Object { $_ -in $productRoles }).Count -gt 0 -and
          @($tokens | Where-Object { $_ -in $publicRoles }).Count -gt 0) {
        Add-Issue -File $relativePath -Line $line -Rule "XAR014" -Message "Kept product controls must not carry a Core role that can change their geometry or states." -Classes $classes
      }
      foreach ($chrome in @("consoleHeaderAction", "panelChromeToggle")) {
        if ($tokenSet.Contains($chrome) -and
            (-not $tokenSet.Contains("actionGhost") -or ($elementName -eq "ToggleButton" -and -not $tokenSet.Contains("flat")))) {
          Add-Issue -File $relativePath -Line $line -Rule "XAR007" -Message "Chrome actions require actionIconButton + actionGhost; state toggles also require flat." -Classes $classes
        }
      }
      if ($elementName -in @("Button", "ToggleButton") -and $tokenSet.Contains("flat") -and -not $tokenSet.Contains("actionGhost")) {
        Add-Issue -File $relativePath -Line $line -Rule "XAR015" -Message "flat is only a modifier of actionGhost." -Classes $classes
      }

      foreach ($token in $tokens) {
        if ($legacyTokens.Contains($token)) {
          Add-Issue `
            -File $relativePath `
            -Line $line `
            -Rule "XAR001" `
            -Message "Legacy action role token '$token' is not allowed in active XAML; use a Core public role or an explicit product class." `
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

      $roleCount = @($tokens | Where-Object { $_ -in @("actionPrimary", "actionNeutral", "actionDanger", "actionGhost", "chipAction") }).Count
      if ($roleCount -gt 1) {
        Add-Issue -File $relativePath -Line $line -Rule "XAR005" -Message "Choose one Core tonal role; actionIconButton is an orthogonal size role." -Classes $classes
      }

      if ($tokenSet.Contains("actionIconButton")) {
        if ($elementName -ne "Button" -and $elementName -ne "ToggleButton") {
          Add-Issue -File $relativePath -Line $line -Rule "XAR006" -Message "actionIconButton is only valid on Button or ToggleButton." -Classes $classes
        }
      }

      foreach ($iconOnlyRole in @("consoleHeaderAction", "panelChromeToggle")) {
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

        if ($tokenSet.Contains("actionChip")) {
          Add-Issue -File $relativePath -Line $line -Rule "XAR012" -Message "chipAction is a complete Core role; the actionChip primitive is removed." -Classes $classes
        }

        if (-not $isDevPreview -and -not (Test-HasAnyAttribute -Element $element -Names $interactiveAttributeNames)) {
          Add-Issue -File $relativePath -Line $line -Rule "XAR013" -Message "chipAction must bind Command or an explicit click/tap handler outside Dev preview samples." -Classes $classes
        }
      }
    }
  }

  $appearance = @("Theme", "Background", "BorderBrush", "BorderThickness", "Foreground", "Template", "CornerRadius", "Width", "Height", "MinHeight", "Padding", "FontFamily", "FontSize", "FontWeight", "Opacity", "FocusAdorner", "Transitions", "ClipToBounds", "HorizontalContentAlignment", "VerticalContentAlignment")
  $mappedRoles = 'actionPrimary|actionNeutral|actionDanger|actionGhost|actionIconButton|chipAction|consoleHeaderAction|panelChromeToggle|simulationActionButton|workspaceActionButton|workspaceGroupActionButton|workspaceHeaderAction|workspaceInfoButton|workspaceSearchClearButton|workspaceSubtleAction|workspaceTextAction|padInfoActionButton|padInfoHeaderActionButton|padInfoInfoButton|padInfoOverrideIconButton|dxfEditMiniAction|dxfEditCompactIconAction|viewMenuButton|iconTextButton|dangerTextButton|confirmDialogActionButton|confirmDialogDangerButton'
  foreach ($file in @($xamlFiles)) {
    $relativePath = Get-RepoRelativePath -Path $file.FullName
    $document = Get-XamlDocument -Path $file.FullName
    foreach ($style in $document.Descendants() | Where-Object { $_.Name.LocalName -eq "Style" }) {
      $selector = Get-AttributeValue -Element $style -Name "Selector"
      if ([string]::IsNullOrWhiteSpace($selector)) { continue }
      $positiveSelector = [regex]::Replace($selector, ':not\([^)]*\)', '')
      if ($positiveSelector -match '\.(actionTextButton|actionChip|actionButton)\b') {
        Add-Issue -File $relativePath -Line (Get-LineNumber -Element $style) -Rule "XAR001" -Message "Styles for removed action primitives must be deleted." -Classes $selector
      }
      $broadRoleStyle = $false
      foreach ($part in ($selector -split ',')) {
        $positivePart = [regex]::Replace($part, ':not\([^)]*\)', '')
        if ($positivePart -match '(?:^|[ >])(Button|ToggleButton)(?=:|$)' -and
            @($publicRoles | Where-Object { -not $part.Contains(":not(.$_)") }).Count -gt 0) {
          $broadRoleStyle = $true
        }
      }
      # A glyph inside a role button is its own control; Core sets no icon size.
      $controlSelector = @($positiveSelector -split ',' | Where-Object { $_ -notmatch 'controls\|FontIcon[.\w]*\s*$' }) -join ','
      $rule = if ($controlSelector -match 'Border\.chipStatus\b') { "XAR103" }
        elseif ($controlSelector -match '(Button|ToggleButton)\.chipAction\b') { "XAR102" }
        elseif ($controlSelector -match "(Button|ToggleButton)\.($mappedRoles)\b" -or $broadRoleStyle) { "XAR101" }
        else { "" }
      if ([string]::IsNullOrEmpty($rule)) { continue }
      foreach ($setter in $style.Elements() | Where-Object { $_.Name.LocalName -eq "Setter" }) {
        $property = Get-AttributeValue -Element $setter -Name "Property"
        if ($property -in $appearance) {
          Add-Issue -File $relativePath -Line (Get-LineNumber -Element $setter) -Rule $rule -Message "NFH must not set '$property' on a Core public role or a mapped action class." -Classes $selector
        }
      }
    }
    foreach ($element in $document.Descendants()) {
      foreach ($attribute in $element.Attributes()) {
        if ($attribute.Name.LocalName -eq "Key" -and $attribute.Value -match '^Nvt\.(Button|Focus)\.') {
          Add-Issue -File $relativePath -Line (Get-LineNumber -Element $element) -Rule "XAR104" -Message "Core button and focus resource overrides are not allowed."
        }
      }
    }
  }
  $appPath = Join-Path $repoRoot "src/FreeformHelper.UI/App.axaml"
  $appDocument = Get-XamlDocument -Path $appPath
  $buttonSource = "avares://Nvt.Core.Avalonia/Theme/ButtonStyles.axaml"
  $appIncludes = @($appDocument.Descendants() | Where-Object { $_.Name.LocalName -eq "StyleInclude" -and (Get-AttributeValue -Element $_ -Name "Source") -eq $buttonSource })
  if ($appIncludes.Count -ne 1) {
    Add-Issue -File "src/FreeformHelper.UI/App.axaml" -Rule "XAR100" -Message "Load Core ButtonStyles exactly once at application level."
  }
  foreach ($file in @($xamlFiles)) {
    $document = Get-XamlDocument -Path $file.FullName
    foreach ($include in $document.Descendants() | Where-Object { $_.Name.LocalName -eq "StyleInclude" }) {
      if ((Get-AttributeValue -Element $include -Name "Source") -eq $buttonSource) {
        Add-Issue -File (Get-RepoRelativePath -Path $file.FullName) -Line (Get-LineNumber -Element $include) -Rule "XAR100" -Message "Remove redundant NFH-scoped Core ButtonStyles includes."
      }
    }
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
