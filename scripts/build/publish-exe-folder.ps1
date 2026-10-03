param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "build/publish"
)

$ErrorActionPreference = "Stop"

& (Join-Path $PSScriptRoot "publish-exe.ps1") `
    -Profile "folder" `
    -Runtime $Runtime `
    -Configuration $Configuration `
    -OutputRoot $OutputRoot
