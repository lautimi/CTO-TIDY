# Comandos `CTO_*` — Referencia

Todos los comandos están en `src/CtoAutocadAddin/Commands/*.cs` y se invocan
desde la línea de comandos de AutoCAD después de `NETLOAD` del DLL.

## Flujo principal

| # | Comando | Archivo | Descripción |
|---|---|---|---|
| 0 | `CTO_SELECCIONAR_POSTES` | `SeleccionarPostesCommand.cs` | Selección manual de postes. Guarda el `ObjectId[]` en `SelectionContext` (singleton) para los siguientes comandos. |
| 1 | `CTO_ASOCIAR_POSTES`     | `AsociarPostesCommand.cs`    | Asocia cada poste al segmento de calle por raycast ortogonal. También asocia frente de manzana y linga más cercana. Escribe: `ID_SEGMENT`, `LARGO`, `ID_FRENTE`, `LARGO_FRENTE`, `ID_LINGA`, `LINGA_TIPO`, `LARGO_LINGA`, `REVISAR`. |
| 2 | `CTO_LEER_COMENTARIOS`   | `LeerComentariosCommand.cs`  | Captura textos en buffer circular alrededor del poste. Escribe: `HP`, `COMENTARIOS`. |
| 4 | `CTO_CALCULAR`           | `CalcularCtosCommand.cs`     | Aplica la tabla oficial (HP × Largo). Agrupa por `ID_SEGMENT`, usa voto mayoritario para `LARGO_FRENTE`, distribuye D-C-D intercalado por mayoría con cap 1D+1C por poste. Escribe: `C_DESP`, `C_CREC`, `C_DESP_OVF`, `C_CREC_OVF`. |
| 5 | `CTO_DESPLEGAR`          | `DesplegarCtosCommand.cs`    | Purga bloques previos en capas `CAJA ACCESO b` y `CAJA ACCESO b-PR` + círculos de alerta (idempotente). Inserta cajas D en `CtoLayerNameDesp` y C en `CtoLayerNameCrec`, rotadas por ángulo de linga (fallback: segmento). Para postes con 1D+1C simultáneo, C se inserta con `extraOffsetY = 3.54m`. Lee overflow `C_DESP_OVF`/`C_CREC_OVF` y los despliega al midpoint del segmento con círculo de alerta r=10. Idem para segmentos sin postes seleccionados. |
| 1-5 | `CTO_RUN_ALL`          | `RunAllCommand.cs`           | Encadena los pasos 1 → 5 en una sola transacción por paso. |

No existe un paso 3 separado — la asociación de frentes y lingas ocurre
dentro del paso 1 para reutilizar la misma pasada geométrica.

## UI / diagnóstico

| Comando | Archivo | Descripción |
|---|---|---|
| `CTO_PANEL`        | `PanelCommand.cs`            | Abre el `PaletteSet` con botones para cada paso + "Ejecutar Todo" + "Inspeccionar poste". Al abrirse, precomputa la `StreetCornerLibrary` con `LoadingOverlay` animado y la cachea en `CtoCache`. |
| `CTO_INSPECCIONAR` | `InspeccionarPosteCommand.cs`| Dumpea toda la XData `KOOVRA_CTO` de un poste seleccionado al Editor. Útil porque `LIST` solo muestra coordenadas. |
| `CTO_ZOOM_HANDLE`  | `ZoomHandleCommand.cs`       | Zoom a una entidad dado su handle hex. Usado desde logs para navegar a segmentos con warning. |
| `CTO_DUMP_CALLES`  | `DumpCallesCommand.cs`       | Debug: selecciona segmentos y reporta los nombres de calle leídos del OD (`CALLE_1`). |
| `CTO_DUMP_ESQUINAS`| `DumpEsquinasCommand.cs`     | Debug: selecciona segmentos, construye `StreetCornerLibrary` y dibuja las esquinas detectadas en la capa `CTO_AUDIT_ESQUINAS` (color 2, amarillo) con texto del par de calles. |

Los comandos debug `CTO_DUMP_CALLES` y `CTO_DUMP_ESQUINAS` se mantienen en Release (no detrás de `#if DEBUG`) para diagnóstico en producción.

### Capas de auditoría V4

| Capa | Color | Contenido |
|---|---|---|
| `CTO_AUDIT_FRENTES_V4` | 3 (verde) | Postes resueltos por V4 (esquinas reales de calle). |
| `CTO_AUDIT_FRENTES_V3` | 6 (cian)  | Postes resueltos por V3 (proyección directa) o V2 (DetectCorners). |
| `CTO_AUDIT_ESQUINAS`   | 2 (amarillo) | Esquinas dibujadas por `CTO_DUMP_ESQUINAS`. |

Activando solo `CTO_AUDIT_FRENTES_V4` se identifica visualmente la cobertura del algoritmo nuevo.

## Settings runtime

Configuración en `Models/AddinSettings.cs` (singleton `AddinSettings.Current`):

- `BlockNameDesp` — nombre del bloque para C_DESP (default: `CAJA_ACCESO_b`).
- `BlockNameCrec` — nombre del bloque para C_CREC (default: `CAJA_CRECIMIENTO`).
- `CtoLayerName`  — capa legacy (no se usa en nuevos deploys).
- `CtoLayerNameDesp` — capa para bloques D (default: `CAJA ACCESO b`).
- `CtoLayerNameCrec` — capa para bloques C (default: `CAJA ACCESO b-PR`).
- `PoleLayerName` — layer del cual `SelectionService` filtra postes. Configurable en runtime.
- `ObservationCodes` — lista de códigos que empujan un poste al final del ranking PRIORIDAD. Ver semilla en `docs/especificacion.md` §9.

| Comando | Archivo | Descripción |
|---|---|---|
| `CTO_CONFIG` | `ConfigCommand.cs` | Abre `SettingsDialog` modal. Permite editar `PoleLayerName` y `ObservationCodes` en memoria. También accesible via botón en `CtoPanel`. Los cambios **no persisten entre sesiones de AutoCAD**. |

## Convenciones

- Todos los comandos usan `CommandFlags.Modal`.
- Todos imprimen info al Editor vía `AcadLogger.Info/Warn/Error`.
- Todos envuelven su lógica en `using (doc.LockDocument()) using (Transaction tr = …)`.
