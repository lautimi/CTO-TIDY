# clean-locks.ps1 - Reporta procesos acad.exe que pueden estar lockeando los DLLs.
# NO mata procesos automaticamente (podria perder trabajo del usuario).
# Uso: powershell -File scripts/clean-locks.ps1
# Nota: solo caracteres ASCII en este script - PowerShell 5.1 lee -File como
# ANSI si no hay BOM, y los emojis/acentos rompen el parseo de strings.

$root = Split-Path -Parent $PSScriptRoot
$dll  = Join-Path $root 'CtoAutocadAddin.dll'

Write-Host "== Procesos acad.exe activos:" -ForegroundColor Cyan
$procs = Get-Process acad -ErrorAction SilentlyContinue
if (-not $procs) {
    Write-Host "  (ninguno)" -ForegroundColor Green
} else {
    $procs | Select-Object Id, ProcessName, StartTime | Format-Table -AutoSize
    Write-Host "  Para matar: Stop-Process -Id <PID>" -ForegroundColor Yellow
}

Write-Host "== Estado del DLL raiz:" -ForegroundColor Cyan
if (Test-Path $dll) {
    Get-Item $dll | Select-Object FullName, LastWriteTime, Length | Format-List
} else {
    Write-Host "  $dll no existe." -ForegroundColor Yellow
}

Write-Host "== Test de lock (apertura exclusiva)..." -ForegroundColor Cyan
if (Test-Path $dll) {
    try {
        $fs = [System.IO.File]::Open($dll, 'Open', 'Read', 'None')
        $fs.Close()
        Write-Host "  [OK] DLL no esta lockeado." -ForegroundColor Green
    } catch {
        Write-Host "  [LOCK] DLL lockeado: $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "  (sin DLL para testear)" -ForegroundColor Yellow
}
