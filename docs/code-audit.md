# Auditoría de código — CTO AutoCAD Add-In

> Auditoría 2026-07-08. Hallazgos sobre UI WinForms (SettingsDialog, CtoPanel,
> FuturisticTheme). Organizado por severidad.

## Alta

1. **GraphicsPath no dispuesto en constructor** — `SettingsDialog.cs:68-74` —
   se asigna a `Region` pero el path no se dispone; en `OnFormResize`
   (109-120) tampoco se dispone el path anterior.
   Fix: guardar región anterior y disponer antes de crear nueva, usar
   `using` o Dispose explícito.
2. **GraphicsPath en OnFormResize sin dispose** — `SettingsDialog.cs:109-120`
   — cada resize crea nuevo path sin descartar el anterior.
   Fix: método helper `RecreateRegion()` que dispone la región vieja antes
   de crear la nueva.
3. **Múltiples Region assignments sin dispose** —
   `FuturisticTheme.cs:355-362, 493-500, 637-644, 821-828, 1022-1027`
   (ChevronButton, SecondaryButton, RunAllButton, DialogButton,
   BtnFuturista) — cada `OnResize` crea Region nueva sin liberar la
   anterior.
   Fix: agregar `path.Dispose()` y `this.Region?.Dispose()` antes de
   asignar.
4. **Timer no garantizado a liberarse en FadeOutOverlay()** —
   `CtoPanel.cs:174-192` — si hay excepción o el form cierra antes de que
   dispare, el timer podría no disponerse.
   Fix: try-finally o guardar referencia local.

## Media-Alta

5. **Event handlers suscritos con lambdas en constructores** —
   `FuturisticTheme.cs:321-324, 459-462, 583-586, 787-790, 976-979` —
   (MouseEnter/Leave/Down/Up) las lambdas capturan `this` implícitamente,
   riesgo de retención si el timer sigue activo.
   Fix: desuscripción explícita en Dispose o métodos nombrados.
6. **Código duplicado: StartHover()/OnHoverTick()** repetida idéntica en 5
   clases de botón (ChevronButton, SecondaryButton, RunAllButton,
   DialogButton, BtnFuturista) —
   `FuturisticTheme.cs:327-353, 465-491, 589-615, 793-819, 984-1010`.
   Fix: extraer a clase base `AnimatedButton : Control`.
7. **StringFormat creado sin `using`** en varios OnPaint —
   `FuturisticTheme.cs:863, 885, 902, 925, 1057, 1096`.
   Fix: envolver en `using (var sf = new StringFormat {...})`.

## Media

8. **BtnFuturista.OnResize dispone el path pero no la Region anterior** —
   `FuturisticTheme.cs:1012-1027`.
   Fix: `this.Region?.Dispose()` antes de asignar nueva Region.
9. **Race condition en StartShimmer() de RunAllButton** —
   `FuturisticTheme.cs:617-635` — llamadas rápidas repetidas podrían
   sobrescribir el timer sin dispose.
   Fix: `_shimmerTimer?.Stop(); _shimmerTimer?.Dispose();` antes de
   reasignar.
10. **ApplyWhiteMatrix no valida ciclo de vida de `src`** —
    `FuturisticTheme.cs:72-91` — si la imagen se descarta antes de
    completar la copia podría haber corrupción.
    Fix: documentar contrato o copiar de forma segura dentro del using.
11. **GetLogoWhite() lazy-load no es thread-safe** —
    `FuturisticTheme.cs:53-70` — estáticos `_logoWhite`/`_logoLoaded` sin
    lock.
    Fix: usar `lock` o `Lazy<Image>`.

## Baja-Media

12. **Timer de OnLayerItemCheck sin tracking** —
    `SettingsDialog.cs:446-457` — timer con Interval=1 creado en cada
    check sin guardar referencia.
    Fix: campo `_deferredUpdateTimer`, Stop/Dispose antes de crear nuevo.
13. **FlashItem() sin protección contra llamadas múltiples** —
    `SettingsDialog.cs:1013-1029` — no verifica si `_flashTimer` ya está
    activo.
    Fix: `_flashTimer?.Stop(); _flashTimer?.Dispose();` antes de crear
    nuevo.

## Baja

14. **Cobertura inconsistente de try-catch en OnPaint** —
    `FuturisticTheme.cs` (varios) — algunos OnPaint tienen try-catch para
    errores GDI+ transitorios, otros no.
    Fix: aplicar uniformemente.
15. **Fuente "Arial" y tamaños hardcodeados** en decenas de lugares sin
    constantes centralizadas — `FuturisticTheme.cs`, `SettingsDialog.cs`,
    `CtoPanel.cs`.
    Fix: crear constantes de tipografía (FONT_LABEL, SIZE_H1, etc.).

## Priorización sugerida

Resolver primero los 4 de Alta (memory leaks directos), luego los de
Media-Alta (deuda técnica/duplicación), el resto son mejoras incrementales
sin urgencia.
