# deploy.ps1 - Copia los DLLs ya compilados a la raiz del proyecto.
# Prefiere bin/; cae a obj/ si bin/ esta lockeado.
# Uso: powershell -File scripts/deploy.ps1
# Nota: solo caracteres ASCII en este script - PowerShell 5.1 lee -File como
# ANSI si no hay BOM, y los emojis/acentos rompen el parseo de strings.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$binDll  = Join-Path $root 'src\CtoAutocadAddin\bin\x64\Release\CtoAutocadAddin.dll'
$objDll  = Join-Path $root 'src\CtoAutocadAddin\obj\x64\Release\CtoAutocadAddin.dll'
$coreDllNet47 = Join-Path $root 'src\CtoAutocadAddin.Core\bin\x64\Release\net47\CtoAutocadAddin.Core.dll'
$coreDllNs20  = Join-Path $root 'src\CtoAutocadAddin.Core\bin\x64\Release\netstandard2.0\CtoAutocadAddin.Core.dll'
$destMain = Join-Path $root 'CtoAutocadAddin.dll'
$destCore = Join-Path $root 'CtoAutocadAddin.Core.dll'

function Copy-Safe($src, $dst) {
    if (-not (Test-Path $src)) { Write-Host "[SKIP] $src no existe."; return $false }
    try {
        Copy-Item $src $dst -Force -ErrorAction Stop
        Write-Host "[OK] $dst  ($(Get-Item $dst | ForEach-Object LastWriteTime))" -ForegroundColor Green
        return $true
    } catch {
        Write-Host "[LOCK] $dst bloqueado." -ForegroundColor Yellow
        return $false
    }
}

$mainOk = Copy-Safe $binDll $destMain
if (-not $mainOk) {
    Write-Host "[RETRY] Reintentando desde obj/..." -ForegroundColor Cyan
    $mainOk = Copy-Safe $objDll $destMain
}
if (-not $mainOk) {
    Write-Host "[FAIL] No se pudo copiar CtoAutocadAddin.dll. Cerra AutoCAD." -ForegroundColor Red
    exit 2
}

$coreDll = if (Test-Path $coreDllNet47) { $coreDllNet47 } else { $coreDllNs20 }
Copy-Safe $coreDll $destCore | Out-Null
Write-Host "Done."
