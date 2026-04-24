# Handoff Spec: SettingsDialog (CTO_CONFIG)

WinForms modal para el Add-In CTO AutoCAD. Estética **futurista celeste + negro**.
Target: **.NET Framework 4.7 / WinForms puro**. Sin WPF, sin animaciones complejas.

## Overview

Modal accesible desde botón en `CtoPanel` y desde el comando `CTO_CONFIG`.
Edita dos settings runtime (no persisten entre sesiones de AutoCAD):

- **Layer de postes** (`AddinSettings.Current.PoleLayerName`).
- **Códigos de observación** (`AddinSettings.Current.ObservationCodes`) — lista editable de strings que penalizan el ranking de postes PRIORIDAD (orden binario: con-obs al final).

## Layout (520 × 480 px)

```
┌────────────────────────────────────────────────────────────┐ 0
│  CONFIGURACIÓN CTO                                     [×] │
│  Settings de sesión (no persisten)                         │ header 72px
├────────────────────────────────────────────────────────────┤
│                                                            │ pad 20
│  LAYER DE POSTES ─────────────────────────────────         │
│  ┌────────────────────────────────────┐ ┌──────────┐       │
│  │ POSTE_TELECOM                   ▾ │ │  ◎ Pick  │       │
│  └────────────────────────────────────┘ └──────────┘       │ sección 92px
│                                                            │
│  CÓDIGOS DE OBSERVACIÓN ───────────────────────────        │
│  Códigos que penalizan ranking de postes PRIORIDAD         │
│  ┌────────────────────────────────────┐ ┌──────────┐       │
│  │ nuevo código…                   ▾ │ │ + Agregar│       │
│  └────────────────────────────────────┘ └──────────┘       │
│  ┌────────────────────────────────────────────────┐        │
│  │ VEG                                             │        │
│  │ FDR                                             │        │
│  │ SUBIDA-BAJADA                                   │        │
│  │ FM                                              │ list   │
│  │ OCUPADO                                         │ 180px  │
│  │ …                                               │        │
│  └────────────────────────────────────────────────┘        │
│                                          ┌──────────┐      │
│                                          │ − Quitar │      │
│                                          └──────────┘      │
│                                                            │
├────────────────────────────────────────────────────────────┤
│ ┌──────────────┐                        ┌────┐ ┌────────┐  │ footer 64px
│ │ ↺ Defaults   │                        │ OK │ │Cancelar│  │
│ └──────────────┘                        └────┘ └────────┘  │
└────────────────────────────────────────────────────────────┘ 480
```

## Design Tokens

| Token | Hex | Usage |
|---|---|---|
| `bg-base` | `#0B1220` | Fondo del form |
| `bg-panel` | `#121A2B` | Fondo de secciones / inputs |
| `bg-panel-hover` | `#1A2540` | Hover en inputs/list items |
| `border-subtle` | `#1E2A44` | Bordes 1px de paneles |
| `border-focus` | `#00BFFF` | Bordes con foco / selección |
| `accent-primary` | `#00BFFF` | Títulos de sección, botón primario, focus |
| `accent-secondary` | `#1E90FF` | Hover sobre primario, gradiente |
| `accent-glow` | `#00BFFF33` | Glow 20% alpha en focus |
| `text-primary` | `#E6F1FF` | Texto principal |
| `text-secondary` | `#8FA3BF` | Subtítulo, labels de sección |
| `text-muted` | `#5A6B85` | Placeholder, disabled |
| `danger` | `#FF5577` | Botón Quitar hover |
| `divider` | `#00BFFF22` | Línea debajo de títulos de sección |

## Typography

Familia: **Segoe UI** (fallback: `Microsoft Sans Serif`).

| Estilo | Size | Weight | Color |
|---|---|---|---|
| Título header | 16pt | Bold | `text-primary` |
| Subtítulo header | 9pt | Regular | `text-secondary` |
| Título de sección | 10pt | Bold + letter-spacing 1px UPPERCASE | `accent-primary` |
| Label | 9pt | Regular | `text-secondary` |
| Input / list item | 10pt | Regular | `text-primary` |
| Botón | 9pt | Semibold UPPERCASE | `text-primary` |

## Components

### Form container

- Size: 520×480. `FormBorderStyle = None` (dibujamos nuestro header).
- `BackColor = bg-base`.
- `OnPaint`: borde exterior 1px `accent-primary` + sombra sutil interna 2px `accent-glow`.
- Draggable desde el header (mouse events).

### Header (72px)

- BG: `bg-base` con gradiente vertical `bg-base → bg-panel` 50%.
- Título "CONFIGURACIÓN CTO" + tag monospace a la derecha `[ CTO_CONFIG ]` 8pt `text-muted`.
- Subtítulo debajo.
- Línea divisora bottom 1px `divider`.
- Botón cerrar `[×]` top-right, 32×32, flat, hover `danger`.

### Section header

- Título UPPERCASE `accent-primary` + línea horizontal `divider` extendida hasta el borde derecho.
- Margen top 16, bottom 8.

### ComboBox (Layer postes — read-only dropdown)

- Size: `380×30`. `FlatStyle = Flat`, `DropDownStyle = DropDownList`.
- `BackColor = bg-panel`, `ForeColor = text-primary`.
- Borde custom (OnPaint del parent): 1px `border-subtle`, on focus `border-focus` + glow `accent-glow` 2px.
- Dropdown arrow: chevron celeste 8×8 custom-drawn.
- Items populados de `LayerTable` del DWG activo.

### ComboBox editable (Códigos)

- Igual que arriba pero `DropDownStyle = DropDown` (permite tipear).
- Placeholder: "nuevo código…" (`text-muted`) cuando vacío (custom draw).
- Sugerencias = `ObservationCodes` defaults (VEG, FDR…).
- Enter = dispara "Agregar".

### Botones

Base: `FlatStyle = Flat`, `Cursor = Hand`, height 32, padding-x 16, border 1px.

| Tipo | Default | Hover | Pressed | Disabled |
|---|---|---|---|---|
| **Primario** (OK, Agregar, Pick) | BG `accent-primary`, text `bg-base`, border `accent-primary` | BG `accent-secondary`, glow 4px `accent-glow` | BG `#0099CC` | BG `border-subtle`, text `text-muted` |
| **Secundario** (Cancelar, Defaults) | BG `transparent`, text `text-primary`, border `border-subtle` | BG `bg-panel-hover`, border `accent-primary` | BG `bg-panel` | text `text-muted` |
| **Peligro** (Quitar) | BG `transparent`, text `text-secondary`, border `border-subtle` | BG `#FF557722`, text `danger`, border `danger` | — | — |

Botones con icono (◎ Pick, + Agregar, − Quitar, ↺ Defaults): icono 14×14 a la izquierda del texto, gap 6px. Iconos custom-drawn en `OnPaint` (formas geométricas simples).

### ListBox (códigos activos)

- Size `380×180`. `BackColor = bg-panel`, `ForeColor = text-primary`.
- `DrawMode = OwnerDrawFixed`, item height 28, padding-x 12.
- Hover: BG `bg-panel-hover`.
- Selected: BG `accent-glow`, barra izquierda 3px `accent-primary`, text `text-primary`.
- Scrollbar custom si es factible; sino default.
- Borde 1px `border-subtle`, on focus `border-focus`.

### Footer (64px)

- BG `bg-panel`, borde top 1px `divider`.
- Izquierda: botón "Defaults" (secundario).
- Derecha: botones "OK" (primario, default) + "Cancelar" (secundario). Gap 8px. Margen right 20.

## Interactions

| Elemento | Trigger | Comportamiento |
|---|---|---|
| Pick | Click | Dialog hace `Hide()`, `ed.GetEntity("Seleccioná una entidad del layer destino…")`, lee `.Layer` de la entidad, actualiza ComboBox, `Show()`. Escape cancela y reabre sin cambios. |
| Agregar | Click / Enter en combo | Si texto no vacío y no duplicado (case-insensitive trim), agrega a ListBox, limpia combo. Si duplicado, el item existente parpadea (flash BG `accent-glow` 200ms). |
| Quitar | Click | Quita el item seleccionado. Disabled si no hay selección. |
| Defaults | Click | Diálogo de confirmación pequeño; si OK, reset a seeds. |
| OK | Click / Enter global | Valida: `PoleLayerName` no vacío. Escribe a `AddinSettings.Current`. Cierra con `DialogResult.OK`. |
| Cancelar | Click / Escape | Cierra sin aplicar. |

## States

| Control | Estado | Apariencia |
|---|---|---|
| ComboBox | Focus | Borde `accent-primary` + glow `accent-glow` |
| ComboBox | Disabled | BG `bg-panel`, text `text-muted` |
| Botón primario | Default | Gradiente `accent-primary → accent-secondary` horizontal sutil |
| Botón primario | Focus (keyboard) | Ring exterior 2px `accent-glow` |
| ListBox item | Empty state | Texto centrado `text-muted`: "Sin códigos. Agregá uno arriba." |
| Form | Loading layers del DWG | ComboBox muestra "Cargando…" `text-muted` durante la lectura de `LayerTable` |

## Edge cases

- **LayerTable vacía**: ComboBox deshabilitado con placeholder "Sin layers en el dibujo". Botón Pick sigue activo.
- **Texto muy largo en lista de códigos**: ellipsis con tooltip completo en hover.
- **Layer seleccionado ya no existe en el DWG**: en OK se muestra mensaje inline rojo y no cierra.
- **Lista de códigos vacía al OK**: permitido — equivale a "ningún código penaliza", ranking vuelve a ser solo por distancia.

## Accessibility (tab order)

1. ComboBox layer postes → 2. Pick → 3. ComboBox códigos → 4. Agregar → 5. ListBox → 6. Quitar → 7. Defaults → 8. OK → 9. Cancelar.

Escape global = Cancelar. Enter global = OK (salvo dentro del ComboBox editable, donde Enter = Agregar).

## Implementation notes (.NET 4.7 WinForms)

- `FormBorderStyle = None` + custom paint del header. `DoubleBuffered = true` en el form y en los paneles custom para evitar flicker.
- Bordes redondeados 4px: `GraphicsPath` con `AddArc` en `OnPaint`, `Region = new Region(path)` para el form.
- Glow: dibujar 2–3 rectángulos concéntricos con alpha decreciente antes del borde principal.
- Gradiente botón: `LinearGradientBrush`.
- Iconos: `Graphics.DrawPolygon` / `DrawArc` en `OnPaint` de un custom `Button` subclass.
- Hover/focus state = campo privado `bool _hover` + `Invalidate()` en `MouseEnter/Leave/GotFocus/LostFocus`.
