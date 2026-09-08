# Auditoría UX/UI — CTO AutoCAD Add-In

> Auditoría 2026-07-08 contra el design system Vezeel Group v4. Organizado
> por severidad.

## Alta

1. **Tipografía: se usa "Arial" directamente** en vez de intentar Open Sans
   primero — `FuturisticTheme.cs:224,231,238,261`; `CtoPanel.cs:255`;
   `SettingsDialog.cs:64` (y más lugares). El design system especifica Open
   Sans como primaria (pesos 300/400/600/700/800) con Arial solo como
   fallback.
   Fix: cargar Open Sans embebida o vía PrivateFontCollection, con fallback
   a Arial.
2. **V-mark de RunAllButton dibujado con Pen directo** en vez de replicar
   el SVG de dos trazos con opacidades 45%/100% del prototipo —
   `FuturisticTheme.cs:701-707`.
   Fix: ajustar grosor/antialiasing para matchear el spec.
3. **Contraste insuficiente**: el log usa TextMuted (#5A7A9A) sobre BgBase
   (#081420), ratio ~3.2:1, bajo el 4.5:1 de WCAG AA — `CtoPanel.cs:294`.
   Fix: cambiar ForeColor del log a TextSecondary (#9AB4CC) o TextPrimary.
4. **Feedback incompleto en botones disabled** (DialogButton
   Primary/Secondary/Danger) — `FuturisticTheme.cs:853-865` — solo 60%
   opacidad, sin Cursor.Default.
   Fix: agregar Cursor=Cursors.Default cuando !Enabled y aumentar
   diferencial de opacidad.

## Media

5. **Falta glow-focus en NumericUpDown de radio** (`_nudRadius`) —
   `CtoPanel.cs:409-431` — el design system especifica glow
   "0 0 0 1px Steel + 0 0 8px rgba(Steel,.22)" en focus, no implementado
   aquí (sí en otros inputs).
   Fix: replicar el glow del prototipo HTML.
6. **Click target de dots de status muy pequeño**: 8×8px vs 48×48
   recomendado (WCAG) — `CtoPanel.cs:1126-1129` (DotIndicator).
   Fix: aumentar a 12×12 o agregar padding de hit-area de 8px.
7. **RunAll() ejecuta los 5 pasos sin espaciado visual** —
   `CtoPanel.cs:511-518` — todos los dots cambian casi simultáneamente,
   dificultando seguir el flujo; el prototipo HTML usa setTimeout
   escalonado (i*680ms).
   Fix: agregar delays entre RunStep calls vía BeginInvoke.
8. **Tamaños de fuente de botones menores al spec**: ChevronButton 8.5f vs
   "sm 12px" esperado, SecondaryButton 9f, RunAllButton main 10f vs
   "base 13px" esperado.
   Fix: ajustar a valores más cercanos al prototipo (ChevronButton 10f,
   SecondaryButton 10f, RunAllButton 11f).
9. **Barra lateral (sidebar) de SecondaryButton muy tenue en estado
   normal**: alpha base 0.45f de Steel — `FuturisticTheme.cs:125-131, 536`.
   Fix: subir alpha base mínimo a 0.65f.
10. **Chevron del step-button puede desalinearse en resize con DPI
    escalado** — `FuturisticTheme.cs:95-108` (MakeChevronPath) — falta
    validar que OnResize recalcula con el ancho final.

## Baja

11. **Falta CancelButton formal en SettingsDialog** —
    `SettingsDialog.cs:336` — solo AcceptButton asignado; Escape funciona
    vía OnKeyDown pero no es binding formal de Windows Forms.
    Fix: agregar `CancelButton = _btnCancel`.
12. **Spinner de LoadingOverlay (3 dots pulsantes) difiere del prototipo
    HTML** (spinner ring tradicional) — `LoadingOverlay.cs:105`.
    Diferencia estética, funcional.
13. **Log no usa fuente mono del design system**: usa "Courier New" 8f en
    vez de JetBrains Mono 11px del prototipo — `CtoPanel.cs:295`.
    Fix: ajustar a JetBrains Mono 9f o Arial 9f si no disponible.
14. **Falta confirmación visual (fade-out) al remover código** en
    SettingsDialog — `SettingsDialog.cs:554-564`. Prioridad muy baja,
    opcional.
15. **Stripe gradient debajo del header no llega a "transparent" real**:
    usa colores opacos en vez de `Color.FromArgb(0,...)` al final del
    blend — `CtoPanel.cs:456-473`.
    Fix: usar alpha 0 en el último color del ColorBlend.

## Priorización sugerida

1) tipografía Open Sans (impacto de marca), 2) contraste del log
(accesibilidad), 3) espaciado de RunAll (usabilidad del flujo crítico),
4) tamaños de fuente de botones (alineación visual), resto son ajustes
incrementales.
