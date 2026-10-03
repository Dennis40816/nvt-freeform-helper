param(
    # Return quietly when example/ is not checked out. Data that is present is still checked in full.
    [switch]$AllowMissing,
    # Before a push: the commit being pushed, not the index, must pin the data, and the data commit must
    # already be on the data repository's remote.
    [switch]$ForPush
)

# Fails with one clear message unless example/ holds the private data at exactly the commit this
# repository pins. Gates call it before building, so a missing or drifted checkout cannot surface later
# as file-not-found errors or pass as skipped tests. Returns $true when the data is present and verified.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$exampleRoot = Join-Path $repoRoot "example"
$hint = "Run 'git submodule update --init example', or pass -AllowMissingExampleData if you have no access."
. (Join-Path $PSScriptRoot "with-temporary-environment.ps1")

# An uninitialized submodule is an empty folder; git commands run inside it would answer for this repository.
if (-not (Test-Path -LiteralPath (Join-Path $exampleRoot ".git"))) {
    # Files without a checkout are data nobody can verify; do not let tests read them.
    $strayEntries = @(Get-ChildItem -LiteralPath $exampleRoot -Force -ErrorAction SilentlyContinue)
    if ($strayEntries.Count -gt 0) {
        throw "example/ contains $($strayEntries.Count) entr$(if ($strayEntries.Count -eq 1) { 'y' } else { 'ies' }) but is not a git checkout. $hint"
    }

    if ($AllowMissing) {
        return $false
    }

    throw "example/ is not checked out. $hint"
}

$pinnedEntry = @(git -C $repoRoot ls-files -s -- example)
if ($LASTEXITCODE -ne 0 -or $pinnedEntry.Count -ne 1 -or -not $pinnedEntry[0].StartsWith("160000 ")) {
    throw "example/ is not tracked as a submodule link in this repository."
}

$pinnedCommit = ($pinnedEntry[0] -split '\s+')[1]
$actualCommit = git -C $exampleRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) {
    throw "Cannot read the commit checked out in example/. $hint"
}

if ($actualCommit -cne $pinnedCommit) {
    throw "example/ is at $actualCommit but this repository pins $pinnedCommit. Run 'git submodule update example', or stage the new pointer if the data change is intended."
}

# Explicit, so a user-level status.showUntrackedFiles=no cannot hide new files.
$changes = @(git -C $exampleRoot status --porcelain --untracked-files=all)
if ($LASTEXITCODE -ne 0) {
    throw "Cannot read the status of example/."
}

if ($changes.Count -gt 0) {
    throw "example/ has $($changes.Count) uncommitted change(s); the gate would verify data other checkouts cannot get."
}

if ($ForPush) {
    git -C $repoRoot diff --cached --quiet -- example
    if ($LASTEXITCODE -ne 0) {
        throw "The example pointer is staged but not committed; the commit being pushed pins different data than the gate verified."
    }

    # Tracking refs are local; refresh and prune them so a branch rewound or deleted on the remote is
    # noticed. A gate must fail, not wait for input, so the prompts git can raise are turned off: its own
    # terminal prompt, the credential manager's dialog, and (unless the caller chose an ssh command) ssh's
    # passphrase and host-key questions.
    $promptSettings = [ordered]@{
        GIT_TERMINAL_PROMPT = "0"
        GCM_INTERACTIVE     = "never"
    }
    if ([string]::IsNullOrEmpty($env:GIT_SSH_COMMAND)) {
        $promptSettings.GIT_SSH_COMMAND = "ssh -o BatchMode=yes"
    }

    $fetchExitCode = Invoke-WithTemporaryEnvironmentVariables -Settings $promptSettings -Action {
        git -C $exampleRoot fetch --prune origin --quiet
        $LASTEXITCODE
    }

    if ($fetchExitCode -ne 0) {
        throw "Could not fetch the data repository's origin remote (offline, no access, or no such remote), so the example data commit cannot be confirmed as published."
    }

    $publishedOn = @(git -C $exampleRoot branch -r --list "origin/*" --contains $actualCommit)
    if ($LASTEXITCODE -ne 0 -or $publishedOn.Count -eq 0) {
        throw "The example data commit $actualCommit is not on the data repository's origin remote; push it there first."
    }
}

return $true
