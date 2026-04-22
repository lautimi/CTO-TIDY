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
