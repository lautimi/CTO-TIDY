# CTO AutoCAD Add-In — Especificación canónica

Spec maestra del proyecto. Migra el workflow QGIS de cálculo y despliegue de
CTOs (Cajas Terminales Ópticas / FTTH) a un Add-In nativo para
**AutoCAD Map 3D 2020**.

---

## 1. Target & Build

- **AutoCAD Map 3D 2020** (ObjectARX 2020, `R23.1`).
- **TargetFramework**: `net47` (requerido por AutoCAD 2020).
- **Platform**: `x64` exclusivamente.
- **LangVersion**: 7.3 (compatible con net47).
- **Output principal**: `src/CtoAutocadAddin/bin/x64/Release/CtoAutocadAddin.dll`.

### Referencias AutoCAD (obligatorias)

En `C:\Program Files\Autodesk\AutoCAD 2020\`:

- `AcMgd.dll` — Application / Document.
- `AcCoreMgd.dll` — Editor / Commands.
- `AcDbMgd.dll` — Transactions / Entities.
- Opcional Map 3D: `AcMapMgd.dll`, `AcMapCore.dll`, `ManagedMapApi.dll`.

### Object Data (Map 3D)

El cálculo de `LARGO_FRENTE` (V4) depende de `ManagedMapApi.dll` (Map 3D 2020), referenciada en el `.csproj` con:

```xml
<HintPath>$(AutocadInstallDir)\Map\ManagedMapApi.dll</HintPath>
```

Lectura de OD en `Map/ObjectDataReader.cs` usando reflexión (`TryReadStringCell`), tolerante a variaciones de la API entre versiones de Map 3D.

**Tabla y campo relevantes:**

| Tabla OD | Campo | Tipo | Descripción |
|---|---|---|---|
| `SEGMENTO` | `CALLE_1` | string | Nombre de la calle del segmento. Valor especial `"CALLE SIN NOMBRE"` para calles sin denominación. |

Si la lectura de OD falla (DLL no disponible, tabla ausente, celda vacía), `CALLE_1` se trata como `"CALLE SIN NOMBRE"` y V4 degrada a V3/V2.

En `.csproj` todas con `<Private>False</Private>` y `<SpecificVersion>False</SpecificVersion>`.

### Deploy

1. Build Release x64 → DLL copiado a la raíz del proyecto por `scripts/build.ps1`.
2. En AutoCAD: `NETLOAD` → elegir `CtoAutocadAddin.dll`.
3. Autoload opcional: registro en
   `HKCU\Software\Autodesk\AutoCAD\R23.1\ACAD-2001:409\Applications\CtoAutocadAddin`
   con `LOADCTRLS=14`, `LOADER=<ruta dll>`, `MANAGED=1`.

---

## 2. Convenciones C#

- **PascalCase** para tipos, métodos, propiedades.
- **camelCase** para locales y parámetros.
- Comandos con prefijo **`CTO_`** en MAYÚSCULAS.
- Namespaces raíz:
  - `Koovra.Cto.AutocadAddin` — lógica con AutoCAD (APIs de Autodesk.AutoCAD.*).
  - `Koovra.Cto.Core` — lógica pura, **sin** referencias a AutoCAD (netstandard2.0).
- `private readonly` cuando el campo no se reasigna.
- `const` solo para literales inmutables en compilación.
- `var` permitido donde el tipo es obvio; firmas públicas siempre con tipo explícito.

---

## 3. Reglas de Transacciones AutoCAD (críticas)

- Toda op de lectura/escritura de DB dentro de
  `using (Transaction tr = db.TransactionManager.StartTransaction())`.
- Abrir objetos con `tr.GetObject(id, OpenMode.ForRead)` (o `ForWrite` si se modifica).
- **NUNCA** cachear `DBObject` fuera del scope de la transacción. Solo `ObjectId`.
- Siempre `tr.Commit()` al final del bloque exitoso; `Dispose` lo maneja `using`.
- Errores no atrapados → rollback automático (comportamiento deseado).
- Comandos que modifican DWG usan `CommandFlags.Modal`.
- Si corre fuera del thread principal, envolver con `doc.LockDocument()`.

---

## 4. Persistencia por Entidad (XData)

- AppName único: **`KOOVRA_CTO`** (registrar en `RegAppTable` al primer uso del DWG).
- Ver catálogo completo en [`xdata-schema.md`](./xdata-schema.md).
- Helper único en `Persistence/XDataManager.cs`.

---

## 5. Logging

- `Editor.WriteMessage("\nmensaje")` — siempre con `\n` inicial.
- Editor vía `Application.DocumentManager.MdiActiveDocument.Editor`.
- Wrapper: `Infrastructure/AcadLogger` (`Info`, `Warn`, `Error`).
- Operaciones largas: `ProgressMeter` (`Start`, `SetLimit`, `MeterProgress`, `Stop`).

---

## 6. Constantes de Geometría

Todas en `Geometry/GeometryConstants.cs`:

| Constante | Valor | Descripción |
|---|---|---|
| `RAY_LENGTH` | 150.0 m | Largo de rayo ortogonal |
| `ANTI_CROSS_MARGIN` | 2.0 m | Margen del filtro anti-cruce |
| `NEAREST_NEIGHBOR_K` | 10 | Candidatos bbox antes de distancia real |
| `EPSILON_DIST` | 0.01 | Distancia mínima válida de una intersección |
| `TEXT_BUFFER_DEFAULT` | 5.0 m | Radio default del buffer de textos |
| `CTO_OFFSET_X` | 2.0 | Offset X al insertar bloque CTO |
| `CTO_OFFSET_Y` | 2.0 | Offset Y al insertar bloque CTO |
| `CTO_SEPARACION` | — | Separación entre cajas del mismo poste |
| `LARGO_CORTE` | 160.0 m | Corte entre ejes cortos/largos en la tabla CTO |
| `CTO_CREC_OFFSET_ADICIONAL` | 3.54 m | Offset perpendicular adicional cuando 1D+1C se insertan en el mismo poste |
| `CTO_ALERT_CIRCLE_RADIUS` | 10.0 m | Radio de los círculos de alerta en capa "0" para cajas overflow/midpoint |

---

## 7. Flujo completo (5 pasos)

Ver [`arquitectura.md`](./arquitectura.md) y [`comandos.md`](./comandos.md).

1. **Asociar postes a segmentos** — `CTO_ASOCIAR_POSTES` (raycast ortogonal).
2. **Leer comentarios** — `CTO_LEER_COMENTARIOS` (buffer circular).
3. **[Asociar frentes/lingas]** — se hace dentro del paso 1 (misma corrida).
4. **Calcular CTOs** — `CTO_CALCULAR` (agrupa por SEGMENTO, tabla HP × Largo).
5. **Desplegar bloques** — `CTO_DESPLEGAR` (purge + insert rotado por ángulo de linga).

Todo el flujo se puede correr de una con `CTO_RUN_ALL` o desde el panel (`CTO_PANEL`).

---

## 8. Testing

- Lógica pura en `CtoAutocadAddin.Core` (netstandard2.0) → `xUnit` sin AutoCAD.
- Integración: smoke test manual sobre `CR001_CORRIENTES_A2_RELEVAMIETNO.dwg`
  (backup previo obligatorio).

---

## 9. Settings runtime

Singleton `AddinSettings.Current` (`Models/AddinSettings.cs`). **No persisten entre sesiones** — viven en memoria y se resetean al cerrar AutoCAD.

### Campos existentes

- `BlockNameDesp` — nombre del bloque para C_DESP (default: `CAJA_ACCESO_b`).
- `BlockNameCrec` — nombre del bloque para C_CREC (default: `CAJA_CRECIMIENTO`).
- `CtoLayerName`  — capa legacy, no se usa en nuevos deploys.
- `CtoLayerNameDesp` — capa para bloques D (default: `CAJA ACCESO b`).
- `CtoLayerNameCrec` — capa para bloques C (default: `CAJA ACCESO b-PR`).

### Campos agregados (desde 2026-04-22)

- `string PoleLayerName` — layer del cual `SelectionService` filtra postes. Default literal del código: no hay un default único en `SelectionService`; el servicio usa `SelectAllOnLayer` con el nombre que le pasan. El campo provee el valor configurable en tiempo de ejecución.
- `List<string> ObservationCodes` — códigos que, si aparecen en el XData `COMMENTS_CSV` de un poste, lo empujan al final del ranking de candidatos PRIORIDAD. Seed inicial (igual a `KnownCodes` en `CommentParser.cs`):

```
"VEG", "FDR", "SUBIDA-BAJADA", "SUBIDA", "BAJADA",
"FM", "OCUPADO", "SC", "APOYO", "INCLINADO",
"PRIORIDAD", "SECUNDARIA", "BUENO", "MALO"
```

### Sub-criterio de ranking en CtoDistributor

> Desde 2026-04-22: postes cuyo `COMMENTS_CSV` contenga al menos un código de `AddinSettings.Current.ObservationCodes` (match case-insensitive, trim, split por coma) se ordenan al final dentro de cada grupo de candidatos PRIORIDAD.

Orden: `OrderBy(tieneObs ? 1 : 0).ThenBy(distMidpoint)`.

**Nunca se descartan** — si faltan candidatos sin observaciones, los observados reciben caja igual.

Implementado en `src/CtoAutocadAddin/Commands/CalcularCtosCommand.cs` (~línea 149).

### Reglas de distribución (Paso 4)

- Cap por poste: máximo **1D + 1C**. Items sobrantes → `ovfD`/`ovfC`.
- `BuildInterleavedSequence` arranca por el tipo mayoritario: `1D+2C → [C,D,C]`, `2D+1C → [D,C,D]`.
- Candidatos: grupo PRIORIDAD → SECUNDARIA → central, ordenados por distancia al midpoint del segmento.
- Los postes seleccionados se reordenan por posición lineal sobre el eje antes de la asignación.
- `polesToUse = min(candidates.Count, sequence.Count)`.
- Radio de asociación de linga: **1m** (`PoleLingaAssociator(maxRadius = 1.0)`).

### Capas hardcoded (no expuestas en UI)

- `"OBSERVACIONES"` — sigue hardcoded en `TextBufferCollector` y `SelectionService`. No se expone en `SettingsDialog`.

---

## 10. Qué NO hacer

- No `async/await` dentro de transacción (COM AutoCAD no es thread-safe).
- No tocar DWG sin `Document.LockDocument()` si estás fuera del thread principal.
- No referenciar `Autodesk.AutoCAD.*` desde `CtoAutocadAddin.Core`.
- No mantener `DBObject` fuera del `using` de la transacción.
- No usar nombres de capa hardcodeados para identificar entidades — el usuario
  elige por selección manual.
