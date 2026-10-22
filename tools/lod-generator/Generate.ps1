param(
    [string]$GameDir = 'E:\SteamLibrary\steamapps\common\Apocalypter',
    [string]$Output = '',
    [string[]]$Include = @(),
    [ValidateSet('Conservative','Balanced','Aggressive')][string]$Preset = 'Balanced',
    [switch]$Install,
    [switch]$Force
)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$GameDir = (Resolve-Path -LiteralPath $GameDir).Path
if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'Apocalypter_Data'))) { throw 'Select the Apocalypter game folder.' }
if (-not $Output) { $Output = Join-Path $taskRoot 'output' }
$Output = [IO.Path]::GetFullPath($Output)
if ($Install -and (Get-Process -Name Apocalypter -ErrorAction SilentlyContinue)) { throw 'Close Apocalypter before installing an LOD pack.' }
Get-Command python,node,npm.cmd -ErrorAction Stop | Out-Null
Push-Location $taskRoot
try {
    if (-not (Test-Path '.venv/Scripts/python.exe')) {
        & python -m venv .venv
        if ($LASTEXITCODE) { throw 'Python environment creation failed.' }
    }
    # Native dependencies must have wheels. tpk-ar is pure Python and publishes only an sdist.
    & ./.venv/Scripts/python.exe -m pip install --disable-pip-version-check --prefer-binary --only-binary=UnityPy,lz4,Pillow,etcpak,astc-encoder-py,texture2ddecoder,brotli,fmod_toolkit -r requirements.txt
    if ($LASTEXITCODE) { throw 'Could not install the pinned Python dependencies.' }
    & npm.cmd ci --ignore-scripts --no-audit --no-fund
    if ($LASTEXITCODE) { throw 'Could not install meshoptimizer.' }
    $extractArgs = @('extract.py', '--game', $GameDir, '--work', (Join-Path $taskRoot 'work'))
    foreach ($assetRoot in $Include) { $extractArgs += @('--include', $assetRoot) }
    if ($Force) { $extractArgs += '--force' }
    & ./.venv/Scripts/python.exe @extractArgs
    if ($LASTEXITCODE) { throw 'Extraction failed; the previous LOD pack has been retained.' }
    & node generate.mjs --work (Join-Path $taskRoot 'work') --output $Output --preset $Preset
    if ($LASTEXITCODE) { throw 'Generation failed; the previous LOD pack has been retained.' }
    if ($Install) {
        if (Get-Process -Name Apocalypter -ErrorAction SilentlyContinue) { throw 'Close Apocalypter before installing the generated pack.' }
        $plugin = Join-Path $GameDir 'BepInEx/plugins/WildWasteland'
        if (-not (Test-Path -LiteralPath (Join-Path $plugin 'WildWasteland.dll'))) { throw 'Wild Wasteland is not active in this game folder.' }
        $disabled = Join-Path $GameDir 'BepInEx/cache/Apocasetter/disabled'
        if (Get-ChildItem -LiteralPath $disabled -Filter manifest.json -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.FullName -match '[\\/]com\.denis\.apocalypter\.wildwasteland[\\/]' }) { throw 'Restore Wild Wasteland through Apocasetter before installing its LOD pack.' }
        $destination = Join-Path $plugin 'Performance/LOD'
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        # Immutable, content-addressed payloads first. Publishing the catalog last commits the new pack.
        $payloads = Get-Content -LiteralPath (Join-Path $Output 'catalog.tsv') | Where-Object { $_.StartsWith("mesh`t") } | ForEach-Object { ($_ -split "`t")[2] } | Sort-Object -Unique
        foreach ($payload in $payloads) {
            if ($payload -notmatch '^[a-f0-9]{64}$') { throw 'Invalid generated payload name.' }
            Copy-Item -LiteralPath (Join-Path $Output "$payload.wwlod") -Destination $destination -Force
        }
        Copy-Item -LiteralPath (Join-Path $Output 'report.json') -Destination $destination -Force
        $catalog = Join-Path $destination 'catalog.next'
        Copy-Item -LiteralPath (Join-Path $Output 'catalog.tsv') -Destination $catalog -Force
        Move-Item -LiteralPath $catalog -Destination (Join-Path $destination 'catalog.tsv') -Force
        Write-Host "Installed LOD pack: $destination"
    }
    Write-Host "Finished. Coverage and exclusions: $Output\report.json"
    Write-Host 'In WW Developer > Performance > Rendering, use a non-Vanilla preset and enable Generated mesh LODs. Restart after replacing a pack.'
} finally { Pop-Location }
