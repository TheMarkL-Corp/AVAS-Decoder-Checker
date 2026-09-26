$ErrorActionPreference = "Stop"
$staging = "pkg_staging"
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Path $staging | Out-Null

Copy-Item "bin\Release\net8.0-windows\*" -Destination $staging -Recurse
New-Item -ItemType Directory -Path (Join-Path $staging "config") | Out-Null
Copy-Item "config\config.default.json" -Destination (Join-Path $staging "config")
New-Item -ItemType Directory -Path (Join-Path $staging "scripts") | Out-Null
Copy-Item "scripts\*" -Destination (Join-Path $staging "scripts") -Recurse -Exclude "package-release.ps1"

if (Test-Path "AVAS-Decoder-Checker-v1.0.3.zip") { Remove-Item "AVAS-Decoder-Checker-v1.0.3.zip" -Force }
if (Test-Path "AVAS-Decoder-Checker.zip") { Remove-Item "AVAS-Decoder-Checker.zip" -Force }

Compress-Archive -Path "$staging\*" -DestinationPath "AVAS-Decoder-Checker-v1.0.3.zip" -Force
Copy-Item "AVAS-Decoder-Checker-v1.0.3.zip" "AVAS-Decoder-Checker.zip" -Force

Remove-Item $staging -Recurse -Force
Get-Item "AVAS-Decoder-Checker-v1.0.3.zip", "AVAS-Decoder-Checker.zip" | Select-Object Name, Length, LastWriteTime
