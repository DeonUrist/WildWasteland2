[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectPath,
    [string]$EditorVersion = '6000.6.6f1',
    [ValidateSet('2d','3d')][string]$Template = '3d',
    [ValidateSet('balanced','coding')][string]$Profile = 'coding',
    [string]$Model,
    [string]$BlenderPath,
    [ValidateRange(4096,131072)][int]$Context = 32768,
    [ValidateRange(1024,65535)][int]$BlenderPort = 9876,
    [switch]$InstallTools,
    [switch]$SkipModel,
    [switch]$Resume,
    [switch]$Plan,
    [switch]$Open
)
$ErrorActionPreference = 'Stop'
$python = Get-Command python -ErrorAction Stop
$setupArgs = @((Join-Path $PSScriptRoot 'setup.py'), '--project', $ProjectPath,
    '--editor-version', $EditorVersion, '--template', $Template, '--profile', $Profile,
    '--context', "$Context", '--blender-port', "$BlenderPort")
if ($Model) { $setupArgs += @('--model', $Model) }
if ($BlenderPath) { $setupArgs += @('--blender-path', $BlenderPath) }
if ($InstallTools) { $setupArgs += '--install-tools' }
if ($SkipModel) { $setupArgs += '--skip-model' }
if ($Resume) { $setupArgs += '--resume' }
if ($Plan) { $setupArgs += '--plan' }
if ($Open) { $setupArgs += '--open' }
& $python.Source @setupArgs
exit $LASTEXITCODE
