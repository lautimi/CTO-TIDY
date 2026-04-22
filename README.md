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

- **`cto-architect`** (Opus) — director del proyecto. Planifica, decide,
  emite directrices. Es el punto de entrada para features y bugs.
- **`cto-implementer`** (Sonnet) — ejecuta las directrices del architect al pie de la letra.
- **`cto-builder`** (Haiku) — compila y deploya.

Flujo típico al pedir una feature:

1. Invocás al `cto-architect` con tu pedido.
2. Architect lee `docs/`, decide el approach, emite una directriz.
3. Architect delega al `cto-implementer` que escribe el código.
4. Implementer llama al `cto-builder` para verificar compilación.
5. Te reportan el resultado con el `NETLOAD` listo para cargar.

## Estructura del repo

```
CTO EN AUTOCAD/
├── CLAUDE.md               # Reglas del proyecto + índice
├── README.md               # Este archivo
├── .claude/agents/         # Definiciones de los 3 agentes
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
