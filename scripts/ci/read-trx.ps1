Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Read-TrxResults {
    param([Parameter(Mandatory = $true)][string]$ResultsDirectory)

    $total = 0
    $passed = 0
    $failed = 0
    $skipped = 0
    $completed = $true
    $results = [System.Collections.Generic.List[object]]::new()
    try {
        $files = @(Get-ChildItem -LiteralPath $ResultsDirectory -Filter *.trx -File -Recurse)
        if ($files.Count -eq 0) { throw "Missing TRX." }
        foreach ($file in $files) {
            $settings = [System.Xml.XmlReaderSettings]::new()
            $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
            $settings.XmlResolver = $null
            $reader = [System.Xml.XmlReader]::Create($file.FullName, $settings)
            try {
                $document = [System.Xml.XmlDocument]::new()
                $document.XmlResolver = $null
                $document.Load($reader)
            } finally {
                $reader.Dispose()
            }
            $ns = [System.Xml.XmlNamespaceManager]::new($document.NameTable)
            $ns.AddNamespace("t", "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")
            $runSummary = $document.SelectSingleNode("/t:TestRun/t:ResultSummary", $ns)
            $completed = $completed -and $null -ne $runSummary -and $runSummary.GetAttribute("outcome") -ceq "Completed"
            $counters = $document.SelectSingleNode("/t:TestRun/t:ResultSummary/t:Counters", $ns)
            if ($null -eq $counters) { throw "Missing counters." }
            $fileCounts = @{}
            foreach ($name in @("total", "passed", "failed")) {
                $value = $counters.GetAttribute($name)
                if ($value -cnotmatch '^[0-9]+$') { throw "Invalid counters." }
                $fileCounts[$name] = [int]$value
            }
            $definitions = @{}
            foreach ($definition in $document.SelectNodes("/t:TestRun/t:TestDefinitions/t:UnitTest", $ns)) {
                $method = $definition.SelectSingleNode("t:TestMethod", $ns)
                if ($null -eq $method) { throw "Missing method." }
                # Never use testName/display names or Output: theory arguments and assertions are private.
                $className = ($method.GetAttribute("className") -split ',', 2)[0].Trim()
                $methodName = ($method.GetAttribute("name") -split '\(', 2)[0].Trim()
                if ($className -cnotmatch '^[A-Za-z0-9_.]+$' -or $methodName -cnotmatch '^[A-Za-z0-9_.]+$') {
                    throw "Invalid method name."
                }
                $definitions[$definition.GetAttribute("id")] = @{
                    ClassName = $className
                    MethodName = "$className.$methodName"
                }
            }
            $fileResults = @($document.SelectNodes("/t:TestRun/t:Results/t:UnitTestResult", $ns))
            $fileSkipped = 0
            $filePassed = 0
            $fileFailed = 0
            foreach ($result in $fileResults) {
                $id = $result.GetAttribute("testId")
                if (-not $definitions.ContainsKey($id)) { throw "Missing definition." }
                $outcome = $result.GetAttribute("outcome")
                switch ($outcome) {
                    "Passed" { $filePassed++ }
                    "Failed" { $fileFailed++ }
                    "NotExecuted" { $fileSkipped++ }
                    default { $outcome = "Incomplete" }
                }
                $results.Add([pscustomobject]@{
                    ClassName = $definitions[$id].ClassName
                    MethodName = $definitions[$id].MethodName
                    Outcome = $outcome
                })
            }
            if ($filePassed -ne $fileCounts.passed -or $fileFailed -ne $fileCounts.failed -or
                $fileResults.Count -gt $fileCounts.total -or
                $fileCounts.passed + $fileCounts.failed + $fileSkipped -gt $fileCounts.total) {
                throw "Inconsistent counters."
            }
            $total += $fileCounts.total
            $passed += $fileCounts.passed
            $failed += $fileCounts.failed
            $skipped += $fileSkipped
        }
    } catch {
        # XML and IO errors can include input text or private paths. Expose only a fixed message.
        throw "TRX results missing or invalid."
    }
    return [pscustomobject]@{
        Completed = $completed
        Total = $total
        Passed = $passed
        Failed = $failed
        Skipped = $skipped
        NotExecuted = $total - $passed - $failed - $skipped
        FailedTests = @($results | Where-Object Outcome -eq "Failed" | ForEach-Object MethodName | Sort-Object -Unique)
        ExecutedClasses = @($results | Where-Object Outcome -in @("Passed", "Failed") | ForEach-Object ClassName | Sort-Object -Unique)
        Results = @($results)
    }
}

function Get-ExampleDataTestNames {
    # Read public test declarations, never example/. Count methods once, independent of theory rows.
    $testRoot = Join-Path $PSScriptRoot "../../tests/FreeformHelper.Tests"
    $pattern = '\[ExampleData(?:Fact|Theory)(?:\([^\]]*\))?\]\s*(?:\[[^\]]*\]\s*)*public\s+(?:async\s+)?(?:void|Task)\s+([A-Za-z0-9_]+)\s*\('
    $names = @(
        foreach ($file in Get-ChildItem -LiteralPath $testRoot -Filter *.cs -File -Recurse) {
            $text = [System.IO.File]::ReadAllText($file.FullName)
            $matches = [regex]::Matches($text, $pattern)
            if ($matches.Count -eq 0) { continue }
            $namespace = [regex]::Match($text, '\bnamespace\s+([A-Za-z0-9_.]+)').Groups[1].Value
            $class = [regex]::Match($text, '\bclass\s+([A-Za-z0-9_]+)').Groups[1].Value
            foreach ($match in $matches) {
                "$namespace.$class.$($match.Groups[1].Value)"
            }
        }
    )
    return @($names | Sort-Object -Unique)
}
