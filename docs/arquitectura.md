# Arquitectura — CTO AutoCAD Add-In

## Capas

```
┌────────────────────────────────────────────────────────────────┐
│ UI / Commands (CtoAutocadAddin/Commands + UI/CtoPanel)         │
│   [CommandMethod("CTO_*")] entry points + PaletteSet panel     │
└──────────────────────┬─────────────────────────────────────────┘
                       │
┌──────────────────────▼─────────────────────────────────────────┐
│ Services (CtoAutocadAddin/Services)                            │
│   AssociationService, CommentReaderService,                    │
│   FrenteManzanaCalculator, CtoBlockDeployer, CtoDistributor    │
└──────────────────────┬─────────────────────────────────────────┘
                       │
┌──────────────────────▼─────────────────────────────────────────┐
│ Geometry (CtoAutocadAddin/Geometry)                            │
│   RayCaster, SpatialIndex, SegmentNormalCalculator,            │
│   GeometryConstants, Extensions                                │
└──────────────────────┬─────────────────────────────────────────┘
                       │
┌──────────────────────▼─────────────────────────────────────────┐
│ Persistence (CtoAutocadAddin/Persistence)                      │
│   XDataManager, XDataKeys, AppNames                            │
└──────────────────────┬─────────────────────────────────────────┘
                       │
┌──────────────────────▼─────────────────────────────────────────┐
│ Core (CtoAutocadAddin.Core, netstandard2.0, SIN AutoCAD)       │
│   CtoCountCalculator, CommentParser, AddressMatcher            │
└────────────────────────────────────────────────────────────────┘
```

## Flujo del usuario (panel / RUN_ALL)

```
┌─────────────────────────┐
│ 0. CTO_SELECCIONAR_POSTES│  Usuario marca los postes objeto.
└───────────┬─────────────┘
            │ guarda ObjectId[] en SelectionContext (singleton)
            ▼
┌─────────────────────────┐
│ 1. CTO_ASOCIAR_POSTES   │  Raycast ortogonal → ID_SEGMENT, LARGO.
│                         │  + Detección de frente de manzana
│                         │    (ID_FRENTE, LARGO_FRENTE).
│                         │  + Asociación de linga por cercanía
│                         │    (ID_LINGA, LINGA_TIPO, LARGO_LINGA).
└───────────┬─────────────┘
            ▼
┌─────────────────────────┐
│ 2. CTO_LEER_COMENTARIOS │  Buffer circular alrededor del poste
│                         │  → COMENTARIOS (CSV), HP (int).
└───────────┬─────────────┘
            ▼
┌─────────────────────────┐
│ 4. CTO_CALCULAR         │  Agrupa postes por ID_SEGMENT.
│                         │  Para cada segmento:
│                         │    - filtra PRIORIDAD
│                         │    - HP del segmento
│                         │    - LARGO_FRENTE (voto mayoritario)
│                         │    - tabla HP × Largo → C_DESP, C_CREC
│                         │    - round-robin D,C,D,C por cercanía al midpoint
└───────────┬─────────────┘
            ▼
┌─────────────────────────┐
│ 5. CTO_DESPLEGAR        │  Purge previa en capa CTO (idempotente)
│                         │  + Insert BlockReference rotado con
│                         │    ángulo de linga (fallback: segmento).
└─────────────────────────┘
```

## Reglas de distribución (Paso 4)

- **Cap por poste**: máximo 1D + 1C por poste. Los items que superan el cap se acumulan como `ovfD`/`ovfC`.
- **BuildInterleavedSequence**: arranca por el tipo mayoritario.
  - `1D+2C → [C,D,C]`
  - `2D+1C → [D,C,D]`
  - `2D+3C → [C,D,C,D,C]`
- **Candidatos**: grupos PRIORIDAD → SECUNDARIA → central, cada grupo ordenado por centralidad (distancia al midpoint del segmento).
- **Postes seleccionados**: reordenados por **posición lineal** sobre el eje del segmento antes de la asignación D-C-D.
- `polesToUse = min(candidates.Count, sequence.Count)`.
- Overflow (`ovfD`, `ovfC`) se escribe en `C_DESP_OVF`/`C_CREC_OVF` al primer poste del segmento (`polesAll[0]`).
- **Radio de linga**: `PoleLingaAssociator(maxRadius = 1.0)` — buffer de 1m alrededor del poste.

## Overflow al midpoint (Paso 5)

1. Paso 5 lee `C_DESP_OVF`/`C_CREC_OVF` del primer poste del segmento.
2. Resuelve la curva del segmento → calcula el midpoint.
3. Llama a `deployer.DeployAtPoint(midPt, ovfD, ovfC, hpSlice, odQueue, rotOvf)` con rotación del CONT_HP.
4. Dibuja un círculo de alerta (r=10, capa "0") en el midpoint.
5. El mismo flujo aplica a segmentos sin postes seleccionados: calcula `cDesp/cCrec` directamente con `CtoCountCalculator.Calculate(hp, largo)` e inserta al midpoint.

## Capas separadas por tipo

| Capa | Setting | Contenido |
|---|---|---|
| `CAJA ACCESO b`    | `CtoLayerNameDesp` | Bloques C_DESP (despliegue inicial). |
| `CAJA ACCESO b-PR` | `CtoLayerNameCrec` | Bloques C_CREC (crecimiento futuro). |

`CtoLayerName` legacy se conserva por compatibilidad pero no se usa en nuevos deploys.

`PurgeExistingBlocks` borra de ambas capas. La operación es idempotente.

## Círculos de alerta

- Radio: `CTO_ALERT_CIRCLE_RADIUS = 10.0` m.
- Capa: `"0"` (capa base de AutoCAD).
- Se insertan en el midpoint del segmento cuando hay overflow o cuando no hay postes seleccionados.
- `PurgeAlertCircles(tr, db)` los elimina antes de cada deploy (idempotente).

## Deuda técnica: duplicación CtoPanel vs DesplegarCtosCommand

`CtoPanel.StepDesplegar` (UI/CtoPanel.cs líneas ~782+) y `DesplegarCtosCommand.Execute` tienen lógica de deploy duplicada. Ambos archivos contienen los features de overflow, midpoint, círculos y capas separadas. **Cualquier cambio futuro al deploy debe sincronizar AMBOS archivos.**

## Unidad de agrupamiento: **SEGMENTO**

Un segmento (eje de calle) = **2 frentes de manzana + 1 bloque CONT_HP**.

- El **HP** está asociado al eje de calle, NO a la linga individual.
- Se reparte en **UN SOLO frente** (el que está marcado como `LINGA_TIPO=PRIORIDAD`).
- Agrupar por linga era un bug: un segmento con 2 lingas PRIORIDAD (una por
  frente) hacía correr la distribución dos veces con el mismo HP → cajas
  duplicadas. Por eso el código agrupa por `ID_SEGMENT` y elige un único
  frente por voto mayoritario.

## Rotación de bloques

`CtoBlockDeployer.ComputeDeploymentAngle` usa:

1. `ID_LINGA` (cable físico real) — preferido.
2. `ID_SEGMENT` (eje abstracto) — fallback.

Ambos almacenan un Handle hex que resuelve a una `Curve`. El ángulo es
`atan2(end.Y - start.Y, end.X - start.X)`.

## Idempotencia

El paso 5 (`CTO_DESPLEGAR`) borra primero todos los `BlockReference` en la
capa CTO. Correr el paso N veces produce el mismo output que correrlo una vez.

## Cálculo de LARGO_FRENTE — V4 (desde 2026-04-22)

### Pipeline V4

1. Lee `CALLE_1` de Object Data (Map 3D), tabla `SEGMENTO`, via `Map/ObjectDataReader.cs`.
2. Construye `StreetCornerLibrary`: indexa endpoints de segmentos por nombre de calle y detecta esquinas en dos fases:
   - **Fase 1 (endpoint matching)**: endpoints de calles distintas a <= `STREET_CORNER_TOLERANCE = 0.5m`.
   - **Fase 2 (intersección geométrica)**: líneas extendidas de pares de segmentos con `CALLE_1` distinto, si ambos endpoints quedan a <= `MAX_INTERSECTION_DIST = 15m` del punto de intersección. Filtra paralelas con `|sin θ| < 0.05`. Deduplica por `{calleA_canon, calleB_canon, round(point/0.5m)}`.
3. Para cada poste: localiza las dos esquinas que delimitan su segmento de calle, las proyecta sobre la polilínea de manzana, y mide el arco entre ellas que contiene la posición del poste.

### Cadena de fallback

`V4 → V3 (proyección directa de endpoints del segmento sobre manzana) → V2 (DetectCorners legacy) → NotFound`

El formato de `ID_FRENTE` indica qué método se usó (ver `xdata-schema.md`).

### Helper SafeGetDistAtPoint

Tres estrategias en cascada para manejar errores de precisión flotante de `GetDistAtPoint`:
1. Llamada directa.
2. Re-proyección del punto sobre la curva.
3. Recorrido manual paramétrico.

### Cache + LoadingOverlay

Al abrir `CTO_PANEL` se precomputa la `StreetCornerLibrary` con un overlay UI animado (`LoadingOverlay`, FuturisticTheme: gradient + 3 dots pulsantes + status text) y se cachea en `CtoCache` (singleton estático). `CTO_ASOCIAR_POSTES` consume el cache si está disponible, evitando rebuild en cada ejecución.

### Capas de auditoría

- `CTO_AUDIT_FRENTES_V4` (color 3, verde): postes resueltos por V4 (esquinas de calle reales).
- `CTO_AUDIT_FRENTES_V3` (color 6, cian): postes resueltos por V3 (proyección directa) o V2 (DetectCorners).

### Constantes V4 (en `Geometry/GeometryConstants.cs`)

| Constante | Valor | Descripción |
|---|---|---|
| `STREET_CORNER_TOLERANCE` | 0.5 m | Distancia max entre endpoints para co-locación (fase 1) |
| `STREET_CORNER_SEARCH_MAX` | 10 m | Distancia max para búsqueda de esquina cerca de endpoint del segmento del poste |
| `CORNER_TO_MANZANA_MAX` | 2 m | Distancia max esquina-de-calle a polilínea de manzana |
| `MAX_INTERSECTION_DIST` | 15 m | Distancia max entre intersección de líneas y endpoint de segmento (fase 2) |

---

## Decisiones históricas

- **LINGA → FRENTE → SEGMENTO** (agrupamiento). La primera iteración agrupaba
  por linga; al descubrir que un eje de calle puede tener 2 lingas PRIORIDAD
  (una por frente), se migró a agrupamiento por segmento con desempate por
  voto mayoritario del ID_FRENTE entre los postes PRIORIDAD del segmento.
- **Purge idempotente en Paso 5**: originalmente el deploy acumulaba bloques
  entre corridas. Ahora la capa CTO se limpia al inicio del paso.
- **Comando `CTO_INSPECCIONAR`**: agregado como diagnóstico — `LIST` en
  AutoCAD solo muestra coordenadas del bloque, no la XData; el nuevo comando
  dumpea todas las claves KOOVRA_CTO del poste seleccionado.

## Módulos críticos

| Módulo | Archivo | Responsabilidad |
|---|---|---|
| Associación | `Services/AssociationService.cs` | Raycast ortogonal poste ↔ segmento |
| Frente manzana | `Services/FrenteManzanaCalculator.cs` | Esquinas, lados, LARGO_FRENTE |
| Lingas | `Services/LingaAssociationService.cs` | Cableado físico (PRIORIDAD/SECUNDARIA) |
| Comentarios | `Services/CommentReaderService.cs` | Buffer circular → HP, COMENTARIOS |
| Distribución | `Commands/CalcularCtosCommand.cs` (`CtoDistributor`) | Tabla → C_DESP, C_CREC + round-robin |
| Deploy | `Services/CtoBlockDeployer.cs` | Purge + Insert bloques rotados |
| Tabla CTO | `CtoAutocadAddin.Core/CtoCountCalculator.cs` | Lookup HP × Largo → (C_DESP, C_CREC) |
