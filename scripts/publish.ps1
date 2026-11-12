param([string]$OutputPath)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (!$OutputPath) { $OutputPath = Join-Path $repo 'artifacts/YoChat-win-x64' }
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
dotnet publish (Join-Path $repo 'src/YoChat/YoChat.csproj') -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -o $OutputPath
if ($LASTEXITCODE -ne 0) { throw 'YoChat publish failed.' }
& (Join-Path $PSScriptRoot 'ensure-wake-model.ps1') -Destination (Join-Path $OutputPath 'models')
Copy-Item -LiteralPath (Join-Path $repo 'README.md'),(Join-Path $repo 'LICENSE'),(Join-Path $repo 'NOTICE.md') -Destination $OutputPath
Copy-Item -LiteralPath (Join-Path $repo 'licenses') -Destination $OutputPath -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repo 'docs') -Destination $OutputPath -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repo 'experimental/README.md') -Destination (Join-Path $OutputPath 'EXPERIMENTAL-README.md') -Force
Write-Host "Portable YoChat is ready: $OutputPath\YoChat.exe"
