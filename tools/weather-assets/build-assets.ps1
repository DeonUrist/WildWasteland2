param(
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\2020.3.49f1\Editor\Unity.exe',
    [switch]$BundleOnly
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $UnityEditor)) { throw "Unity editor missing: $UnityEditor" }
$project = Join-Path $PSScriptRoot 'Unity'
$log = Join-Path $PSScriptRoot 'asset-build.log'
# No -nographics: this authoring build renders a preview using the GPU.
$method = if ($BundleOnly) { 'WeatherAssetBuild.BuildBundle' } else { 'WeatherAssetBuild.Build' }
$arguments = @('-batchmode', '-quit', '-projectPath', ('"' + $project + '"'), '-executeMethod', $method, '-logFile', ('"' + $log + '"'))
$assetProcess = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if ($assetProcess.ExitCode -ne 0) { throw "Asset build failed. Read $log" }
Write-Output "Asset build complete. See Output and asset-build.log. Nothing deployed to the game."
