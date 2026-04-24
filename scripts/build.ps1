# build.ps1 — Compila CtoAutocadAddin en Release x64 y copia los DLLs a la raíz.
# Uso: powershell -File scripts/build.ps1
# Requiere: dotnet SDK en PATH.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

Write-Host "Compiling CtoAutocadAddin (Release x64)..." -ForegroundColor Cyan
$proj = Join-Path $root 'src\CtoAutocadAddin\CtoAutocadAddin.csproj'
dotnet build $proj -c Release -p:Platform=x64 -v:m --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build FAILED (exit $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}

$binDll  = Join-Path $root 'src\CtoAutocadAddin\bin\x64\Release\CtoAutocadAddin.dll'
$objDll  = Join-Path $root 'src\CtoAutocadAddin\obj\x64\Release\CtoAutocadAddin.dll'
$destMain = Join-Path $root 'CtoAutocadAddin.dll'

# Main DLL: intentar bin/, fallback a obj/
$src = if (Test-Path $binDll) { $binDll } elseif (Test-Path $objDll) { $objDll } else { $null }
if (-not $src) {
    Write-Host "ERROR: No se encontro CtoAutocadAddin.dll ni en bin/ ni en obj/" -ForegroundColor Red
    exit 1
}

try {
    Copy-Item $src $destMain -Force -ErrorAction Stop
    $lmt = (Get-Item $destMain).LastWriteTime
    Write-Host "OK. DLL copied. LastWriteTime: $lmt" -ForegroundColor Green
} catch {
    Write-Host "LOCK. CtoAutocadAddin.dll locked by AutoCAD. Close AutoCAD and retry, or use scripts/deploy.ps1." -ForegroundColor Yellow
    exit 2
}

$oldCore = Join-Path $root 'CtoAutocadAddin.Core.dll'
if (Test-Path $oldCore) { Remove-Item $oldCore -Force -ErrorAction SilentlyContinue }

Write-Host "Success. DLL ready for NETLOAD: CtoAutocadAddin.dll" -ForegroundColor Green
