param(
    [string]$Configuration = "Release",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $root "McpForUnityAgent.csproj"

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $root "artifacts\McpForUnityAgent"
}

$outputFullPath = [IO.Path]::GetFullPath($OutputDir)
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $root "artifacts"))
if (-not $outputFullPath.StartsWith($artifactsRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean output outside artifacts: $outputFullPath"
}

if (Test-Path -LiteralPath $OutputDir) {
    Remove-Item -LiteralPath $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir | Out-Null

dotnet build $projectPath -c $Configuration
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$exePath = Join-Path $root "bin\$Configuration\net40\McpForUnityAgent.exe"
$quickstartPptx = Join-Path $root "docs\McpForUnityAgent-quickstart-ko.pptx"
if (-not (Test-Path -LiteralPath $quickstartPptx)) {
    throw "Quickstart PowerPoint not found: $quickstartPptx. Run scripts\generate-docs.ps1 before packaging."
}

Copy-Item -LiteralPath $exePath -Destination (Join-Path $OutputDir "McpForUnityAgent.exe")
Copy-Item -LiteralPath (Join-Path $root "README.md") -Destination (Join-Path $OutputDir "README.md")

$versionInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($exePath)
$version = $versionInfo.ProductVersion
if ([string]::IsNullOrWhiteSpace($version)) {
    $version = $versionInfo.FileVersion
}
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Could not determine package version from $exePath"
}
Set-Content -Path (Join-Path $OutputDir "VERSION.txt") -Value "McpForUnity Agent $version" -Encoding ASCII

Set-Content -Path (Join-Path $OutputDir "install.cmd") -Value "@echo off`r`nsetlocal`r`nstart `"`" `"%~dp0McpForUnityAgent.exe`" --install`r`n" -Encoding ASCII
Set-Content -Path (Join-Path $OutputDir "uninstall.cmd") -Value "@echo off`r`nsetlocal`r`nstart `"`" `"%~dp0McpForUnityAgent.exe`" --uninstall`r`n" -Encoding ASCII

$docsDir = Join-Path $OutputDir "docs"
New-Item -ItemType Directory -Path $docsDir | Out-Null
Copy-Item -LiteralPath $quickstartPptx -Destination (Join-Path $docsDir "McpForUnityAgent-quickstart-ko.pptx")

$sourceDir = Join-Path $OutputDir "src"
New-Item -ItemType Directory -Path $sourceDir | Out-Null
Copy-Item -LiteralPath (Join-Path $root "McpForUnityAgent") -Destination $sourceDir -Recurse
Copy-Item -LiteralPath (Join-Path $root "Properties") -Destination $sourceDir -Recurse
Copy-Item -LiteralPath (Join-Path $root "app.ico") -Destination $sourceDir
Copy-Item -LiteralPath $projectPath -Destination (Join-Path $sourceDir "McpForUnityAgent.csproj")

$zipPath = Join-Path $root "artifacts\McpForUnityAgent.zip"
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -LiteralPath @(
    (Join-Path $OutputDir "McpForUnityAgent.exe"),
    (Join-Path $OutputDir "install.cmd"),
    (Join-Path $OutputDir "uninstall.cmd"),
    (Join-Path $OutputDir "README.md"),
    (Join-Path $OutputDir "VERSION.txt"),
    $docsDir,
    $sourceDir
) -DestinationPath $zipPath -Force

Write-Host "Package written to $OutputDir"
Write-Host "Zip written to $zipPath"
