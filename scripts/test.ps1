$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
dotnet run --project (Join-Path $repo 'tests/YoChat.Tests/YoChat.Tests.csproj') --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'YoChat tests failed.' }
