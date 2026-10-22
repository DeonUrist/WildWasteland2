param([int]$Size = 512, [int]$Samples = 128, [int]$Clouds = 4, [int]$Views = 2)
$ErrorActionPreference = 'Stop'
$blender = Join-Path $PSScriptRoot 'Tools\blender-4.5.3-windows-x64\blender.exe'
if (-not (Test-Path -LiteralPath $blender)) { throw 'Run python prepare-blender.py first.' }
$script = Join-Path $PSScriptRoot 'bake-clouds.py'
$arguments = @('--background', '--factory-startup', '--python-exit-code', '1', '--python', ('"' + $script + '"'), '--', '--size', $Size, '--samples', $Samples, '--clouds', $Clouds, '--views', $Views)
$bakeProcess = Start-Process -FilePath $blender -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput (Join-Path $PSScriptRoot 'cloud-bake.log') -RedirectStandardError (Join-Path $PSScriptRoot 'cloud-bake-errors.log')
if ($bakeProcess.ExitCode -ne 0) { throw 'Cloud bake failed. Read cloud-bake.log and cloud-bake-errors.log.' }
Write-Output 'Cloud bake complete. Static variants are under Bakes\cloud-stamps.'
