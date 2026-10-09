$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $taskCompiler)) { $taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe' }
$taskTests = Join-Path $taskRoot 'build/tests'
New-Item -ItemType Directory -Path $taskTests -Force | Out-Null
foreach ($taskModule in 'PetState','RoamMotion','RageQuotes','DailyProgress','SpeechSchedule','WhipRope','WhipChoreography','WhipSound') {
    $taskSources = @((Get-Item -LiteralPath "$taskRoot/src/$taskModule.cs").FullName, (Get-Item -LiteralPath "$taskRoot/tests/${taskModule}Tests.cs").FullName)
    $taskExe = Join-Path $taskTests "${taskModule}Tests.exe"
    & $taskCompiler /nologo /codepage:65001 /r:System.Drawing.dll "/out:$taskExe" $taskSources
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $taskModule" }
    & $taskExe
    if ($LASTEXITCODE -ne 0) { throw "Test failed: $taskModule" }
}
$taskSmokePath = Join-Path $taskTests 'runtime-check.txt'
$taskSmoke = Start-Process -FilePath "$taskRoot/build/XWhip.exe" -ArgumentList @('--smoke-test', ('"' + $taskSmokePath + '"')) -PassThru -Wait -WindowStyle Hidden
if ($taskSmoke.ExitCode -ne 0) { throw 'Windows runtime smoke test failed.' }
Get-Content -LiteralPath $taskSmokePath
