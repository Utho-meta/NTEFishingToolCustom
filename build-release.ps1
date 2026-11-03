param(
    [string]$Version = "v1.0.10"
)

$ErrorActionPreference = "Stop"

$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$OutputRoot = Join-Path (Split-Path -Parent $ProjectDir) "github-release"
$PackageName = "NTEFishingToolCustom-$Version-win-x64"
$PublishDir = Join-Path $OutputRoot $PackageName
$ZipPath = Join-Path $OutputRoot "$PackageName.zip"

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

if (Test-Path $PublishDir) {
    Remove-Item -LiteralPath $PublishDir -Recurse -Force
}

if (Test-Path $ZipPath) {
    Remove-Item -LiteralPath $ZipPath -Force
}

& dotnet publish (Join-Path $ProjectDir "NTEFishingToolCustom.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $PublishDir `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$RuntimeLogs = @(
    "NTEFishingToolCustom.log",
    "LoopException.log"
)

foreach ($LogName in $RuntimeLogs) {
    $LogPath = Join-Path $PublishDir $LogName
    if (Test-Path $LogPath) {
        Remove-Item -LiteralPath $LogPath -Force
    }
}

Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $ZipPath -Force

Write-Host "Release folder: $PublishDir"
Write-Host "Release zip:    $ZipPath"
