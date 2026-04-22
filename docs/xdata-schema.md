# XData Schema — `KOOVRA_CTO`

Todas las entidades "poste" del DWG llevan XData bajo el AppName único
**`KOOVRA_CTO`** (registrado en `RegAppTable` al primer uso). El acceso va
siempre por `Persistence/XDataManager.cs`. Las claves están definidas como
constantes en `Persistence/AppNames.cs` (clase `XDataKeys`).

## Catálogo

| Clave | Tipo | Paso | Descripción |
|---|---|---|---|
| `ID_SEGMENT`   | string (handle hex) | 1 | Segmento (eje de calle) al que se asoció el poste por raycast ortogonal. |
| `REVISAR`      | string              | 1 | `OK` \| `REVISAR` \| `SIN_CALLE_POSTE` \| `SIN_SEGMENTO`. |
| `LARGO`        | real (m)            | 1 | Largo del segmento asociado. |
| `ID_LINGA`     | string (handle hex) | 1 | Linga (cable físico) más cercana al poste. |
| `LINGA_TIPO`   | string              | 1 | `PRIORIDAD` \| `SECUNDARIA` \| `""`. Solo PRIORIDAD recibe cajas. |
| `LARGO_LINGA`  | real (m)            | 1 | Largo real de la linga en metros. |
| `ID_FRENTE`    | string              | 1 | `"<manzanaHandle>#<frenteIdx>"`. Frente de manzana donde está el poste. |
| `LARGO_FRENTE` | real (m)            | 1 | Largo del lado de la manzana entre dos esquinas (lo que entra en la tabla CTO). |
| `HP`           | int32               | 2 | Hogares Pasados (proyección Futura 100%) leídos del buffer de textos. |
| `COMENTARIOS`  | string CSV          | 2 | Textos capturados del buffer del poste. |
| `C_DESP`       | int16               | 4 | Cajas Despliegue Inicial 40% (resultado de la tabla). |
| `C_CREC`       | int16               | 4 | Cajas Crecimiento Futuro 100% (resultado de la tabla). |

## Valores válidos de `LINGA_TIPO`

Constantes en `XDataKeys`:

- `LINGA_PRIORIDAD = "PRIORIDAD"` — el frente de este poste recibe las cajas.
- `LINGA_SECUNDARIA = "SECUNDARIA"` — poste no recibe cajas (C_DESP=0, C_CREC=0).
- `""` (vacío) — poste sin linga asociada → también 0,0.

## Relación entre campos

- `LARGO_LINGA` es informativo (auditoría del cableado). **El valor que entra
  en la tabla CTO es `LARGO_FRENTE`**, no `LARGO_LINGA`.
- `ID_FRENTE` identifica el lado de manzana. Dos postes del mismo segmento
  pueden estar en frentes distintos (uno por cada lado del eje) — el paso 4
  usa voto mayoritario entre los postes PRIORIDAD del segmento para decidir
  el `LARGO_FRENTE` que aplica.
- `ID_SEGMENT` agrupa la distribución de CTOs. Un segmento = 2 frentes + 1
  bloque CONT_HP. El HP del segmento se toma de cualquier poste PRIORIDAD
  (todos comparten el mismo, por ser del eje de calle).

## Acceso desde código

```csharp
// Lectura
string segId = XDataManager.GetString(tr, poleId, XDataKeys.ID_SEGMENT);
int    hp    = XDataManager.GetInt   (tr, poleId, XDataKeys.HP) ?? 0;
double largo = XDataManager.GetReal  (tr, poleId, XDataKeys.LARGO_FRENTE) ?? 0.0;

// Escritura (siempre en bloque)
XDataManager.SetValues(tr, poleId, new (string, object)[]
{
    (XDataKeys.C_DESP, (object)cDesp),
    (XDataKeys.C_CREC, (object)cCrec),
});
```

## Diagnóstico

Comando `CTO_INSPECCIONAR`: selecciona un poste y dumpea todas las claves
KOOVRA_CTO al Editor. Útil porque `LIST` de AutoCAD solo muestra coordenadas.
