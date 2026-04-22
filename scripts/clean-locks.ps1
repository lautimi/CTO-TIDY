# clean-locks.ps1 — Reporta procesos acad.exe que pueden estar lockeando los DLLs.
# NO mata procesos automáticamente (podría perder trabajo del usuario).
# Uso: powershell -File scripts/clean-locks.ps1

$root = Split-Path -Parent $PSScriptRoot
$dll  = Join-Path $root 'CtoAutocadAddin.dll'

Write-Host "→ Procesos acad.exe activos:" -ForegroundColor Cyan
$procs = Get-Process acad -ErrorAction SilentlyContinue
if (-not $procs) {
    Write-Host "  (ninguno)" -ForegroundColor Green
} else {
    $procs | Select-Object Id, ProcessName, StartTime | Format-Table -AutoSize
    Write-Host "  Para matar: Stop-Process -Id <PID>" -ForegroundColor Yellow
}

Write-Host "→ Estado del DLL raíz:" -ForegroundColor Cyan
if (Test-Path $dll) {
    Get-Item $dll | Select-Object FullName, LastWriteTime, Length | Format-List
} else {
    Write-Host "  $dll no existe." -ForegroundColor Yellow
}

Write-Host "→ Intentando abrir el DLL en modo exclusivo (test de lock)..." -ForegroundColor Cyan
try {
    $fs = [System.IO.File]::Open($dll, 'Open', 'Read', 'None')
    $fs.Close()
    Write-Host "  ✅ DLL no está lockeado." -ForegroundColor Green
} catch {
    Write-Host "  🔒 DLL lockeado: $_" -ForegroundColor Yellow
}
