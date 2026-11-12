param([Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference = 'Stop'
$name = 'vosk-model-small-en-us-0.15'
$hash = '30F26242C4EB449F948E42CB302DD7A686CB29A3423A8367F99FF41780942498'
$cache = Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/wake-model-cache'
$archive = Join-Path $cache 'model.zip'
New-Item -ItemType Directory -Path $cache -Force | Out-Null
if (!(Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive).Hash -ne $hash) {
    $partial = Join-Path $cache 'model.zip.part'
    Invoke-WebRequest -Uri "https://alphacephei.com/vosk/models/$name.zip" -OutFile $partial -TimeoutSec 180
    if ((Get-FileHash -LiteralPath $partial).Hash -ne $hash) { throw 'Wake model checksum mismatch.' }
    Move-Item -LiteralPath $partial -Destination $archive -Force
}
# Validate the fixed publisher archive before extracting it to the model folder.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    foreach ($entry in $zip.Entries) {
        if (!$entry.FullName.StartsWith("$name/") -or $entry.FullName -match '\.\.|\\|:') {
            throw 'Unexpected model archive path.'
        }
    }
} finally { $zip.Dispose() }
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
[IO.Compression.ZipFile]::ExtractToDirectory($archive, [IO.Path]::GetFullPath($Destination), $true)
if (!(Test-Path -LiteralPath (Join-Path $Destination "$name/am/final.mdl"))) { throw 'Wake model extraction failed.' }
Write-Host 'Verified local wake model is ready.'
