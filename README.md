# CTO AutoCAD Add-In

Add-In nativo para **AutoCAD Map 3D 2020** que automatiza el cálculo y
despliegue de CTOs (Cajas Terminales Ópticas / FTTH).

Migra el workflow que corría en QGIS a un flujo dentro de AutoCAD, usando
XData para persistencia y la tabla oficial del cliente (HP × Largo) para
dimensionar las cajas.

## Quick start

La DLL ya viene pre-compilada en la raíz del repo (`CtoAutocadAddin.dll`).

```
# En AutoCAD:
NETLOAD            # elegir CtoAutocadAddin.dll de la raíz del repo
CTO_PANEL          # abre el panel con todos los pasos
```

### Recompilar (si modificás el código)

```powershell
powershell -File scripts/build.ps1
```

El script compila en Release x64 y actualiza la DLL en la raíz.

## Documentación

Toda la especificación vive en `docs/`:

- [`docs/especificacion.md`](docs/especificacion.md) — spec maestra.
- [`docs/arquitectura.md`](docs/arquitectura.md) — capas + flujo 1→5.
- [`docs/xdata-schema.md`](docs/xdata-schema.md) — catálogo XData.
- [`docs/tabla-ctos.md`](docs/tabla-ctos.md) — tabla oficial.
- [`docs/comandos.md`](docs/comandos.md) — comandos `CTO_*`.
- [`docs/glosario.md`](docs/glosario.md) — términos del dominio.

## Trabajando con Claude Code

Este repo tiene un **sistema multi-agente** en `.claude/agents/`:

- **`sifon`** (Opus) — director del proyecto. Planifica, decide, emite
  directrices. Es el punto de entrada para features y bugs.
- **`paredes`** (Sonnet) — ejecuta las directrices de sifon al pie de la letra.
- **`delgado`** (Haiku) — compila y deploya.
- **`ander`** (Sonnet) — mantiene `docs/` actualizados con nuevas specs y
  decisiones, para que no se pierda contexto entre sesiones.
- **`roman`** (Haiku) — maneja `git` y `gh`: commits, branches, push/pull,
  PRs, issues. Con guardrails contra ops destructivas.

Flujo típico al pedir una feature:

1. Invocás a `sifon` con tu pedido.
2. Sifon lee `docs/`, decide el approach, emite una directriz.
3. Si la decisión introduce info nueva (regla, comando, campo XData), sifon
   delega a `ander` para persistirla en `docs/`.
4. Sifon delega a `paredes` que escribe el código.
5. Paredes llama a `delgado` para verificar compilación.
6. Sifon delega a `roman` para commit + push + PR.
7. Te reportan el resultado con el `NETLOAD` listo para cargar.

### Backlog

Las features pendientes y bugs abiertos viven como [GitHub Issues](https://github.com/lautimi/CTO-TIDY/issues).
Al arrancar una sesión, podés pedirle al architect `cerrá el issue #N`.

## Estructura del repo

```
CTO EN AUTOCAD/
├── CLAUDE.md               # Reglas del proyecto + índice
├── README.md               # Este archivo
├── .claude/agents/         # Definiciones de los 5 agentes (sifon, paredes, delgado, ander, roman)
├── docs/                   # Especificación canónica
├── scripts/                # build.ps1, deploy.ps1, clean-locks.ps1
├── src/
│   ├── CtoAutocadAddin/        # Lógica con AutoCAD (net47, x64)
│   └── CtoAutocadAddin.Core/   # Lógica pura (netstandard2.0)
├── tests/                  # xUnit sobre Core
└── CR001_…A2_RELEVAMIETNO.dwg  # DWG de prueba
```

## Requisitos

- Windows + AutoCAD Map 3D 2020.
- .NET SDK (dotnet CLI).
- Las referencias AutoCAD se toman directo de
  `C:\Program Files\Autodesk\AutoCAD 2020\` — no son NuGet.
