# deploy.ps1 — Copia los DLLs ya compilados a la raíz del proyecto.
# Prefiere bin/; cae a obj/ si bin/ está lockeado.
# Uso: powershell -File scripts/deploy.ps1

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$binDll  = Join-Path $root 'src\CtoAutocadAddin\bin\x64\Release\CtoAutocadAddin.dll'
$objDll  = Join-Path $root 'src\CtoAutocadAddin\obj\x64\Release\CtoAutocadAddin.dll'
$coreDll = Join-Path $root 'src\CtoAutocadAddin.Core\bin\x64\Release\netstandard2.0\CtoAutocadAddin.Core.dll'
$destMain = Join-Path $root 'CtoAutocadAddin.dll'
$destCore = Join-Path $root 'CtoAutocadAddin.Core.dll'

function Copy-Safe($src, $dst) {
    if (-not (Test-Path $src)) { Write-Host "⚠  $src no existe, skip."; return $false }
    try {
        Copy-Item $src $dst -Force -ErrorAction Stop
        Write-Host "✅ $dst  ($(Get-Item $dst | ForEach-Object LastWriteTime))" -ForegroundColor Green
        return $true
    } catch {
        Write-Host "🔒 $dst LOCKED." -ForegroundColor Yellow
        return $false
    }
}

$mainOk = Copy-Safe $binDll $destMain
if (-not $mainOk) {
    Write-Host "→ Reintentando desde obj/..." -ForegroundColor Cyan
    $mainOk = Copy-Safe $objDll $destMain
}
if (-not $mainOk) {
    Write-Host "❌ No se pudo copiar CtoAutocadAddin.dll. Cerrá AutoCAD." -ForegroundColor Red
    exit 2
}

Copy-Safe $coreDll $destCore | Out-Null
Write-Host "Done."
