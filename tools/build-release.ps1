# Builds the release exe into dist\ and prints its SHA-256 for the release notes.
# Usage: powershell -File tools\build-release.ps1
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$dist = Join-Path $root 'dist'

dotnet build (Join-Path $root 'FastSnip.csproj') -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

[xml]$proj = Get-Content (Join-Path $root 'FastSnip.csproj')
$version = $proj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $dist | Out-Null
$exe = Join-Path $dist 'FastSnip.exe'
Copy-Item (Join-Path $root 'bin\Release\net48\FastSnip.exe') $exe

$hash = (Get-FileHash $exe -Algorithm SHA256).Hash
"FastSnip v$version"
"  $exe ($([int]((Get-Item $exe).Length / 1KB)) KB)"
"  SHA-256: $hash"
