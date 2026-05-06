# CTO AutoCAD Add-In — Instalación

## Requisitos

- AutoCAD Map 3D 2020 (64-bit)
- .NET Framework 4.7

## Instalación

1. Copiar el archivo **`CtoAutocadAddin.dll`** a cualquier carpeta local.
   (Es un único DLL — el módulo Core va embebido como recurso, ya no es necesario distribuirlo aparte.)

2. Abrir AutoCAD Map 3D 2020.

3. En la línea de comandos escribir:
   ```
   NETLOAD
   ```

4. Seleccionar el archivo `CtoAutocadAddin.dll`.

5. Escribir `CTO_PANEL` para abrir el panel de workflow.

## Uso

El panel tiene 5 pasos en orden:

| Paso | Comando | Descripción |
|---|---|---|
| 1 | Seleccionar postes | Detecta todos los postes en el DWG |
| 2 | Asociar postes | Asocia cada poste a su segmento de calle, frente de manzana y linga (radio 1m) |
| 3 | Leer comentarios (HP) | Lee HP y comentarios de los bloques de texto cercanos |
| 4 | Calcular CTOs | Calcula la distribución de CTOs por segmento (max 1D + 1C por poste) |
| 5 | Desplegar CTOs | Inserta los bloques CTO en el DWG |

Usar **"EJECUTAR TODO"** para correr los 5 pasos en secuencia.

## Capas de salida

| Tipo | Capa |
|---|---|
| Caja Despliegue | `CAJA ACCESO b` |
| Caja Crecimiento | `CAJA ACCESO b-PR` |
| Círculos de alerta (cajas no acomodadas en postes) | `0` (radio 10) |

Las capas se crean automáticamente si no existen.

## Casos especiales en Paso 5

- **1D + 1C en mismo poste**: ambas cajas comparten posición X, la C va con offset perpendicular adicional de 3.54m.
- **Overflow** (más cajas que postes disponibles en un segmento): las sobrantes se insertan en el **midpoint del segmento** y quedan marcadas con un círculo rojo de radio 10 en capa "0".
- **Segmento sin postes seleccionados** (con CONT_HP existente): las cajas se calculan desde HP+LARGO y se insertan en el midpoint, también con círculo de alerta.
- Las cajas insertadas al midpoint heredan la rotación del bloque `CONT_HP` del segmento.

## Notas

- Si después del Paso 2 aparece una sección amarilla **"⚠ N postes en esquina"**, son postes donde el frente fue corregido automáticamente. Hacer click en cada hipervínculo para revisarlos visualmente.
- Comando de diagnóstico: `CTO_INSPECCIONAR` (click sobre un poste para ver sus datos).
- Object Data CAJA_ACCESO con campos `ACRÓNIMO`, `HP_EJE`, `ID_SEGMENTO` se asigna automáticamente a las cajas Despliegue.
- Idempotencia: re-ejecutar Paso 5 N veces produce el mismo output (purga bloques + círculos previos).
