# build.ps1 — Compila CtoAutocadAddin en Release x64 y copia los DLLs a la raíz.
# Uso: powershell -File scripts/build.ps1
# Requiere: dotnet SDK en PATH.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

Write-Host "→ dotnet build CtoAutocadAddin (Release x64)..." -ForegroundColor Cyan
$proj = Join-Path $root 'src\CtoAutocadAddin\CtoAutocadAddin.csproj'
dotnet build $proj -c Release -p:Platform=x64 -v:m --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build FAILED (exit $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}

$binDll  = Join-Path $root 'src\CtoAutocadAddin\bin\x64\Release\CtoAutocadAddin.dll'
$objDll  = Join-Path $root 'src\CtoAutocadAddin\obj\x64\Release\CtoAutocadAddin.dll'
$coreDll = Join-Path $root 'src\CtoAutocadAddin.Core\bin\x64\Release\netstandard2.0\CtoAutocadAddin.Core.dll'
$destMain = Join-Path $root 'CtoAutocadAddin.dll'
$destCore = Join-Path $root 'CtoAutocadAddin.Core.dll'

# Main DLL: intentar bin/, fallback a obj/
$src = if (Test-Path $binDll) { $binDll } elseif (Test-Path $objDll) { $objDll } else { $null }
if (-not $src) {
    Write-Host "❌ No se encontró CtoAutocadAddin.dll ni en bin/ ni en obj/" -ForegroundColor Red
    exit 1
}

try {
    Copy-Item $src $destMain -Force -ErrorAction Stop
    Write-Host "✅ $destMain  ($(Get-Item $destMain | ForEach-Object LastWriteTime))" -ForegroundColor Green
} catch {
    Write-Host "🔒 $destMain LOCKED por AutoCAD. Cerralo y reintentá, o usá scripts/deploy.ps1." -ForegroundColor Yellow
    exit 2
}

# Core DLL
if (Test-Path $coreDll) {
    try {
        Copy-Item $coreDll $destCore -Force -ErrorAction Stop
        Write-Host "✅ $destCore  ($(Get-Item $destCore | ForEach-Object LastWriteTime))" -ForegroundColor Green
    } catch {
        Write-Host "⚠  $destCore no se pudo copiar (puede estar lockeado). Continuando." -ForegroundColor Yellow
    }
}

Write-Host "Done."
