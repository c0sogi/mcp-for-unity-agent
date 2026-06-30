param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "",
    [string]$PptxPath = "",
    [string]$ManualPptxPath = "",
    [string]$PresentationSkillDir = "",
    [switch]$SkipBuild,
    [switch]$SkipScreenshots,
    [switch]$Regenerate
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $root "docs"
}
if ([string]::IsNullOrWhiteSpace($PptxPath)) {
    $PptxPath = Join-Path $OutputDir "McpForUnityAgent-quickstart-ko.pptx"
}

$buildDir = Join-Path $root "artifacts\docs-build"
$assetDir = Join-Path $buildDir "assets\quickstart-ko"
$previewDir = Join-Path $buildDir "preview"
$projectPath = Join-Path $root "McpForUnityAgent.csproj"
$exePath = Join-Path $root "bin\$Configuration\net40\McpForUnityAgent.exe"

function Copy-ManualDeck {
    param(
        [string]$SourcePath,
        [string]$DestinationPath
    )

    if (-not (Test-Path -LiteralPath $SourcePath)) {
        throw "Manual PowerPoint not found: $SourcePath"
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $DestinationPath) | Out-Null
    Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force
    Write-Host "Manual PowerPoint copied to $DestinationPath"
}

function Resolve-PresentationSkillDir {
    param([string]$Requested)

    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace($Requested)) {
        $candidates += $Requested
    }
    if (-not [string]::IsNullOrWhiteSpace($env:PRESENTATIONS_SKILL_DIR)) {
        $candidates += $env:PRESENTATIONS_SKILL_DIR
    }

    $cacheRoot = Join-Path $env:USERPROFILE ".codex\plugins\cache\openai-primary-runtime\presentations"
    if (Test-Path -LiteralPath $cacheRoot) {
        Get-ChildItem -LiteralPath $cacheRoot -Directory |
            Sort-Object LastWriteTime -Descending |
            ForEach-Object {
                $candidates += (Join-Path $_.FullName "skills\presentations")
            }
    }

    foreach ($candidate in $candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) {
            continue
        }
        $setupScript = Join-Path $candidate "container_tools\setup_artifact_tool_workspace.mjs"
        if (Test-Path -LiteralPath $setupScript) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "Could not find the presentations skill directory. Pass -PresentationSkillDir or set PRESENTATIONS_SKILL_DIR."
}

function Save-UnityMcpReadmeImage {
    param([string]$TargetDir)

    $imagePath = Join-Path $TargetDir "00-unity-mcp-building-scene.gif"
    $imageUrl = "https://raw.githubusercontent.com/CoplayDev/unity-mcp/beta/docs/images/building_scene.gif"
    try {
        Invoke-WebRequest -Uri $imageUrl -OutFile $imagePath
    }
    catch {
        if (-not (Test-Path -LiteralPath $imagePath)) {
            throw "Could not download Unity MCP README demo image from $imageUrl. $($_.Exception.Message)"
        }
        Write-Warning "Could not refresh Unity MCP README demo image. Using existing file: $imagePath"
    }
}

if (-not [string]::IsNullOrWhiteSpace($ManualPptxPath)) {
    Copy-ManualDeck -SourcePath $ManualPptxPath -DestinationPath $PptxPath
    return
}

if ((Test-Path -LiteralPath $PptxPath) -and -not $Regenerate) {
    Write-Host "Existing PowerPoint kept: $PptxPath"
    Write-Host "Pass -Regenerate to rebuild it from screenshots and script."
    return
}

if (-not $SkipBuild) {
    dotnet build $projectPath -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "McpForUnityAgent.exe not found: $exePath"
}

New-Item -ItemType Directory -Force -Path $OutputDir, $assetDir, $previewDir | Out-Null
Save-UnityMcpReadmeImage $assetDir

if (-not $SkipScreenshots) {
    powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "capture-ui-screenshots.ps1") `
        -Configuration $Configuration `
        -ExePath $exePath `
        -OutputDir $assetDir
}

$node = Get-Command node -ErrorAction SilentlyContinue
if (-not $node) {
    throw "Node.js is required to generate the PowerPoint deck."
}

$skillDir = Resolve-PresentationSkillDir $PresentationSkillDir
$setupScript = Join-Path $skillDir "container_tools\setup_artifact_tool_workspace.mjs"
$workDir = Join-Path $buildDir "_artifact-tool-workspace"
New-Item -ItemType Directory -Force -Path $workDir | Out-Null

node $setupScript --workspace $workDir
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$generatorSource = Join-Path $PSScriptRoot "generate-quickstart-ppt.mjs"
$generatorTarget = Join-Path $workDir "generate-quickstart-ppt.mjs"
Copy-Item -LiteralPath $generatorSource -Destination $generatorTarget -Force

node $generatorTarget `
    --repo-root $root `
    --asset-dir $assetDir `
    --out $PptxPath `
    --preview-dir $previewDir
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$inspectPath = "$PptxPath.inspect.ndjson"
if (Test-Path -LiteralPath $inspectPath) {
    Move-Item -LiteralPath $inspectPath -Destination (Join-Path $buildDir (Split-Path -Leaf $inspectPath)) -Force
}

Write-Host "PowerPoint written to $PptxPath"
Write-Host "Preview images written to $previewDir"
