$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root "bin\Release\net40\McpForUnityAgent.exe"

if (-not (Test-Path -LiteralPath $exe)) {
    dotnet build (Join-Path $root "McpForUnityAgent.csproj") -c Release
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$out = Join-Path $env:TEMP "mcp-for-unity-agent-dependency-check.out.txt"
$err = Join-Path $env:TEMP "mcp-for-unity-agent-dependency-check.err.txt"
Remove-Item -LiteralPath $out, $err -ErrorAction SilentlyContinue

$process = Start-Process -FilePath $exe `
    -ArgumentList "--dependency-check --simulate-missing-dependencies" `
    -NoNewWindow `
    -Wait `
    -PassThru `
    -RedirectStandardOutput $out `
    -RedirectStandardError $err

if (Test-Path -LiteralPath $out) {
    Get-Content -Raw -LiteralPath $out
}
if (Test-Path -LiteralPath $err) {
    Get-Content -Raw -LiteralPath $err | Write-Error
}
exit $process.ExitCode
