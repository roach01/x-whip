param([string]$Version = '0.4.0')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $taskCompiler)) { $taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe' }
$taskBuild = Join-Path $taskRoot 'build'
New-Item -ItemType Directory -Path $taskBuild -Force | Out-Null
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must be major.minor.patch.' }
$taskVersionFile = Join-Path $taskBuild 'AssemblyVersion.cs'
Set-Content -LiteralPath $taskVersionFile -Value ('[assembly:System.Reflection.AssemblyVersion("' + $Version + '.0")][assembly:System.Reflection.AssemblyFileVersion("' + $Version + '.0")]') -Encoding UTF8
$taskSources = @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName }) + @($taskVersionFile)
& $taskCompiler /nologo /codepage:65001 /optimize+ /target:winexe /platform:anycpu "/out:$taskBuild/XWhip.exe" "/win32icon:$taskRoot/assets/app.ico" "/resource:$taskRoot/assets/faces.png,Faces" /r:System.Windows.Forms.dll /r:System.Drawing.dll $taskSources
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
Write-Output "Built XWhip.exe $Version"
