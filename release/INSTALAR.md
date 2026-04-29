# CTO AutoCAD Add-In — Instalación

## Requisitos

- AutoCAD Map 3D 2020 (64-bit)
- .NET Framework 4.7

## Instalación

1. Copiar **ambos** archivos DLL a cualquier carpeta local:
   - `CtoAutocadAddin.dll`
   - `CtoAutocadAddin.Core.dll`

2. Abrir AutoCAD Map 3D 2020.

3. En la línea de comandos escribir:
   ```
   NETLOAD
   ```

4. Seleccionar el archivo `CtoAutocadAddin.dll` (la carpeta que contiene ambos DLLs).

5. Escribir `CTO_PANEL` para abrir el panel de workflow.

## Uso

El panel tiene 5 pasos en orden:

| Paso | Comando | Descripción |
|---|---|---|
| 1 | Seleccionar postes | Detecta todos los postes en el DWG |
| 2 | Asociar postes | Asocia cada poste a su segmento de calle y frente de manzana |
| 3 | Leer comentarios (HP) | Lee HP y comentarios de los bloques de texto cercanos |
| 4 | Calcular CTOs | Calcula la distribución de CTOs por frente |
| 5 | Desplegar CTOs | Inserta los bloques CTO en el DWG |

Usar **"EJECUTAR TODO"** para correr los 5 pasos en secuencia.

## Notas

- Si después del Paso 2 aparece una sección amarilla **"⚠ N postes en esquina"**, son postes donde el frente fue corregido automáticamente. Hacer click en cada hipervínculo para revisarlos visualmente.
- Comando de diagnóstico: `CTO_INSPECCIONAR` (click sobre un poste para ver sus datos).
