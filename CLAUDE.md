# CLAUDE.md — CTO AutoCAD Add-In

Add-In nativo para **AutoCAD Map 3D 2020** que migra el workflow de cálculo y
despliegue de CTOs (Cajas Terminales Ópticas / FTTH) desde QGIS.

## Docs canónicas (leer antes de trabajar)

Toda la especificación vive en `docs/`. Usá esos archivos como fuente de verdad:

- [`docs/especificacion.md`](docs/especificacion.md) — spec completa: target, build, convenciones C#, transacciones, logging, constantes.
- [`docs/arquitectura.md`](docs/arquitectura.md) — capas, flujo 1→5, decisiones históricas.
- [`docs/xdata-schema.md`](docs/xdata-schema.md) — catálogo XData `KOOVRA_CTO`.
- [`docs/tabla-ctos.md`](docs/tabla-ctos.md) — tabla oficial HP × Largo.
- [`docs/comandos.md`](docs/comandos.md) — comandos `CTO_*`.
- [`docs/glosario.md`](docs/glosario.md) — segmento, manzana, frente, linga, HP…

## Arquitectura multi-agente

Los agentes viven en `.claude/agents/` y se invocan con el Agent tool.

| Agente | Rol | Modelo |
|---|---|---|
| `cto-architect`    | Director — lee specs, diseña, emite directrices. No escribe `.cs`. | Opus |
| `cto-implementer`  | Ejecutor — implementa directrices exactas del architect. | Sonnet |
| `cto-builder`      | Build & deploy — corre `scripts/build.ps1`, copia DLLs. | Haiku |
| `cto-doc-keeper`   | Mantiene `docs/` actualizados con nuevas specs/decisiones. | Sonnet |

Flujo típico: usuario → architect → implementer → builder → usuario.
Cuando surge una decisión nueva que hay que persistir: architect → doc-keeper.

Para features o cambios arquitectónicos, siempre empezar por `cto-architect`.

## Scripts

- `scripts/build.ps1` — compila Release x64 y copia DLLs a raíz.
- `scripts/deploy.ps1` — solo copia DLLs (si ya compilaste).
- `scripts/clean-locks.ps1` — diagnostica locks de AutoCAD sobre el DLL.

## Reglas rápidas (resumen — para detalle ver `docs/especificacion.md`)

- **net47 + x64** obligatorio. LangVersion 7.3.
- PascalCase / camelCase; comandos con prefijo `CTO_`.
- Namespaces: `Koovra.Cto.AutocadAddin` (con AutoCAD) y `Koovra.Cto.Core` (sin AutoCAD).
- Toda op de DB dentro de `using (Transaction tr = db.TransactionManager.StartTransaction())`.
- Nunca cachear `DBObject` fuera de la transacción — solo `ObjectId`.
- XData siempre vía `Persistence/XDataManager.cs`, claves en `XDataKeys`.
- Lógica pura en `CtoAutocadAddin.Core` — **sin** `Autodesk.AutoCAD.*`.

## Deploy

1. `powershell -File scripts/build.ps1`
2. En AutoCAD: `NETLOAD` → elegir `CtoAutocadAddin.dll` de la raíz.
3. `CTO_PANEL` para abrir el panel con los pasos.
