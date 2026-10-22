[CmdletBinding()]
param([string]$ConfigDirectory = (Join-Path $env:USERPROFILE '.config\opencode'))
$ErrorActionPreference = 'Stop'
$sourceDirectory = Join-Path $PSScriptRoot 'global'
New-Item -ItemType Directory -Path $ConfigDirectory -Force | Out-Null
$backupDirectory = Join-Path $ConfigDirectory ('backups\unity-workflow-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$globalInstructions = Join-Path $ConfigDirectory 'AGENTS.md'
$instructions = Get-Content -Raw -LiteralPath (Join-Path $sourceDirectory 'AGENTS.md')
$marker = '<!-- unity-verification-workflow -->'
if (Test-Path -LiteralPath $globalInstructions) {
    $existing = Get-Content -Raw -LiteralPath $globalInstructions
    if (-not $existing.Contains($marker)) {
        New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
        Copy-Item -LiteralPath $globalInstructions -Destination (Join-Path $backupDirectory 'AGENTS.md')
        [IO.File]::WriteAllText($globalInstructions, $existing.TrimEnd() + "`n`n" + $marker + "`n" + $instructions)
    }
} else {
    [IO.File]::WriteAllText($globalInstructions, $marker + "`n" + $instructions)
}
$skillSource = Join-Path $sourceDirectory 'skills\unity-verification'
$skillTarget = Join-Path $ConfigDirectory 'skills\unity-verification'
foreach ($file in Get-ChildItem -LiteralPath $skillSource -File -Recurse) {
    $relative = $file.FullName.Substring($skillSource.Length).TrimStart('\','/')
    $target = Join-Path $skillTarget $relative
    if (Test-Path -LiteralPath $target) {
        $backup = Join-Path $backupDirectory (Join-Path 'unity-verification' $relative)
        New-Item -ItemType Directory -Path (Split-Path $backup) -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination $backup
    }
    New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target
}
Write-Output "Installed global Unity workflow: $ConfigDirectory"
Write-Output 'Reload OpenCode configuration to discover the new skill. Existing projects retain their own settings.'
