param([Parameter(Mandatory=$true)][string]$UnityData)
$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$build = Join-Path ([IO.Path]::GetTempPath()) ('telegraph-validation-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $build | Out-Null
$dotnet = Join-Path $UnityData 'NetCoreRuntime/dotnet.exe'
$csc = Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'
$refs = Get-ChildItem (Join-Path $UnityData 'NetStandard/ref/2.1.0/*.dll') | ForEach-Object { '-r:"' + $_.FullName + '"' }
$sources = @('Assets/Scripts/Card.cs','Assets/Scripts/RandomEnemyTelegraph.cs') | ForEach-Object { '"' + (Join-Path $repo $_) + '"' }
$compile = @('-nologo','-target:library','-define:PURGATORY_INTERFACE_CHECKS','-langversion:9','-nostdlib+',('-out:"'+(Join-Path $build 'Telegraph.dll')+'"')) + $refs
$compile += Get-ChildItem (Join-Path $UnityData 'Managed/UnityEngine/*.dll') | ForEach-Object { '-r:"' + $_.FullName + '"' }
$compile += $sources + @(('"'+(Join-Path $repo 'Assets/Scripts/CardAttackVisuals.cs')+'"'),('"'+(Join-Path $PSScriptRoot 'Interfaces.cs')+'"'))
$rsp = Join-Path $build 'compile.rsp'; $compile | Set-Content $rsp
& $dotnet $csc ('@'+$rsp)
if ($LASTEXITCODE -ne 0) { throw 'Unity-reference compilation failed' }
$checks = @('-nologo','-target:exe','-define:PURGATORY_BEHAVIOR_CHECKS','-langversion:9','-nostdlib+',('-out:"'+(Join-Path $build 'BehaviorChecks.dll')+'"')) + $refs + $sources + ('"'+(Join-Path $PSScriptRoot 'BehaviorChecks.cs')+'"')
$rsp = Join-Path $build 'checks.rsp'; $checks | Set-Content $rsp
& $dotnet $csc ('@'+$rsp)
if ($LASTEXITCODE -ne 0) { throw 'Behavior-check compilation failed' }
Copy-Item (Join-Path $UnityData 'DotNetSdkRoslyn/csc.runtimeconfig.json') (Join-Path $build 'BehaviorChecks.runtimeconfig.json')
& $dotnet (Join-Path $build 'BehaviorChecks.dll')
if ($LASTEXITCODE -ne 0) { throw 'Behavior checks failed' }
Write-Output "Validation artifacts: $build"
