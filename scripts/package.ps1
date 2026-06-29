param(
    [string]$Configuration = "Release",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $root "McpForUnityAgent.csproj"

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $root "artifacts"
}

$outputFullPath = [IO.Path]::GetFullPath($OutputDir)
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $root "artifacts"))
if (-not $outputFullPath.StartsWith($artifactsRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to write installer outside artifacts: $outputFullPath"
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

dotnet build $projectPath -c $Configuration
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$exePath = Join-Path $root "bin\$Configuration\net40\McpForUnityAgent.exe"
$setupPath = Join-Path $OutputDir "McpForUnityAgentSetup.exe"
if (Test-Path -LiteralPath $setupPath) {
    Remove-Item -LiteralPath $setupPath -Force
}
Copy-Item -LiteralPath $exePath -Destination $setupPath

$versionInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($exePath)
$version = $versionInfo.ProductVersion
if ([string]::IsNullOrWhiteSpace($version)) {
    $version = $versionInfo.FileVersion
}
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Could not determine package version from $exePath"
}

Write-Host "Installer written to $setupPath"
Write-Host "Version: McpForUnity Agent $version"
