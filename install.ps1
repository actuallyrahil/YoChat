param([switch]$StartOnLogin, [switch]$NoStart, [switch]$NoShortcuts, [string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA 'Programs/YoChat'))
$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne 'Win32NT') { throw 'YoChat requires Windows.' }
if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Install the .NET 10 SDK first: winget install --exact --id Microsoft.DotNet.SDK.10'
}
if (!(dotnet --list-sdks | Where-Object { $_ -match '^10\.' })) { throw 'The .NET 10 SDK is required to build YoChat.' }
if (Get-Process -Name YoChat -ErrorAction SilentlyContinue) { throw 'Exit YoChat from its tray menu before installing an update.' }
$stage = Join-Path ([IO.Path]::GetTempPath()) ('YoChat-publish-' + [Guid]::NewGuid().ToString('N'))
& (Join-Path $PSScriptRoot 'scripts/test.ps1')
& (Join-Path $PSScriptRoot 'scripts/publish.ps1') -OutputPath $stage
New-Item -ItemType Directory -Path $InstallDirectory -Force | Out-Null
Copy-Item -Path (Join-Path $stage '*') -Destination $InstallDirectory -Recurse -Force
$exe = Join-Path $InstallDirectory 'YoChat.exe'
function Add-YoChatShortcut([string]$folder) {
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut((Join-Path $folder 'YoChat.lnk'))
    $link.TargetPath = $exe
    $link.WorkingDirectory = $InstallDirectory
    $link.Save()
}
if (!$NoShortcuts) { Add-YoChatShortcut ([Environment]::GetFolderPath('Programs')) }
if ($StartOnLogin) { Add-YoChatShortcut ([Environment]::GetFolderPath('Startup')) }
Write-Host "Installed YoChat to $InstallDirectory"
if (!$NoStart) { Start-Process -FilePath $exe -WindowStyle Hidden }
