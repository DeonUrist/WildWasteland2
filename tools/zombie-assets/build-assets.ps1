param(
    [string]$Downloads = 'C:\Downloads',
    [string]$Project = 'V:\Repos\Apocalypter\_tools\WWZombieAssets',
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\2020.3.49f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
foreach ($folder in @('Assets\Editor', 'Assets\Source', 'Assets\Clips', 'Packages', 'ProjectSettings', 'Build')) {
    New-Item -ItemType Directory -Path (Join-Path $Project $folder) -Force | Out-Null
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ZombieAssetBuild.cs') -Destination (Join-Path $Project 'Assets\Editor\ZombieAssetBuild.cs')
Set-Content -LiteralPath (Join-Path $Project 'Packages\manifest.json') -Value '{"dependencies":{"com.unity.modules.animation":"1.0.0","com.unity.modules.assetbundle":"1.0.0"}}'
Set-Content -LiteralPath (Join-Path $Project 'ProjectSettings\ProjectVersion.txt') -Value 'm_EditorVersion: 2020.3.49f1'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zombieZip = [IO.Compression.ZipFile]::OpenRead((Join-Path $Downloads 'Scary Zombie Pack.zip'))
try {
    foreach ($entry in $zombieZip.Entries) {
        if ($entry.Name -eq 'X Bot.fbx' -or $entry.Name -eq 'zombie death.fbx' -or $entry.Name -notlike '*.fbx') { continue }
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $Project ('Assets\Source\' + $entry.Name)), $true)
    }
} finally { $zombieZip.Dispose() }
foreach ($name in @('Zombie Stand Up.fbx', 'Zombie Stand Up (1).fbx', 'Zombie Agonizing.fbx', 'Zombie Idle.fbx', 'Zombie Scream.fbx', 'Zombie Crawl.fbx', 'Zombie_Run.fbx', 'Zombie_Stand_Up.fbx')) {
    Copy-Item -LiteralPath (Join-Path $Downloads $name) -Destination (Join-Path $Project ('Assets\Source\' + $name))
}
$zombieLog = Join-Path $Project 'Build\asset-build.log'
$zombieArgs = @('-batchmode', '-nographics', '-quit', '-projectPath', ('"' + $Project + '"'), '-executeMethod', 'ZombieAssetBuild.Build', '-logFile', ('"' + $zombieLog + '"'))
$zombieProcess = Start-Process -FilePath $UnityEditor -ArgumentList $zombieArgs -WindowStyle Hidden -Wait -PassThru
if ($zombieProcess.ExitCode -ne 0) { throw "Zombie asset authoring failed: $zombieLog" }
$zombieTarget = Join-Path $PSScriptRoot '..\..\src\Modules\Presentation\Embedded\Models\zombie-anims-v3.bundle'
Copy-Item -LiteralPath (Join-Path $Project 'Build\zombie-anims-v3.bundle') -Destination $zombieTarget
Write-Output "Zombie bundle authored: $zombieTarget. Log: $zombieLog"
