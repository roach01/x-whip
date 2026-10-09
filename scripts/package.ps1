param([string]$Version = '0.6.0', [string]$Iscc)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskDist = Join-Path $taskRoot 'dist'
$taskPortable = Join-Path $taskDist 'XWhip'
New-Item -ItemType Directory -Path $taskPortable -Force | Out-Null
Copy-Item -LiteralPath "$taskRoot/build/XWhip.exe","$taskRoot/README.md" -Destination $taskPortable
Copy-Item -LiteralPath "$taskRoot/assets/PHOTO-CREDITS.md" -Destination $taskPortable
Compress-Archive -LiteralPath $taskPortable -DestinationPath "$taskDist/XWhip-Portable-$Version.zip" -Force
if (-not $Iscc) {
    $Iscc = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'
    if (-not (Test-Path -LiteralPath $Iscc)) { throw 'Provide -Iscc pointing to the Inno Setup 6 compiler.' }
}
& $Iscc "/DAppVersion=$Version" "/O$taskDist" "$taskRoot/installer/XWhip.iss"
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
Get-ChildItem -LiteralPath $taskDist -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | ForEach-Object { '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLower(), $_.Name } | Set-Content -LiteralPath "$taskDist/SHA256SUMS.txt" -Encoding UTF8
