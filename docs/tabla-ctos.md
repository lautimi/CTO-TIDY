# Tabla Oficial de CTOs

Definida por el cliente (Koovra). Lookup 2D: **HP (Hogares Pasados, Futuro
100%) × Largo del frente**. Máximo 4 cajas por columna.

Implementación: `CtoAutocadAddin.Core/CtoCountCalculator.cs`.
Corte de Largo: `GeometryConstants.LARGO_CORTE = 160.0 m`.

| HP (Futuro 100%) | ≤160m C_DESP | ≤160m C_CREC | >160m C_DESP | >160m C_CREC |
|---|---|---|---|---|
| 1–2   | 0 | 1 | 0 | 0 |
| 3–5   | 1 | 0 | 1 | 0 |
| 6–8   | 1 | 0 | 2 | 0 |
| 9–16  | 1 | 1 | 2 | 0 |
| 17–20 | 1 | 2 | 2 | 1 |
| 21–24 | 2 | 1 | 2 | 1 |
| 25–32 | 2 | 2 | 2 | 2 |
| 33–40 | 2 | 3 | 2 | 3 |
| 41–48 | 3 | 3 | 3 | 3 |
| 49–56 | 3 | 4 | 3 | 4 |
| 57–64 | 4 | 4 | 4 | 4 |

## Fuera de rango

- HP > 64: se usa el **último rango** (57–64) y se emite warning en el log.
  `CtoCountCalculator.IsOutOfRange(hp)` devuelve `true` para HP > 64.
- HP ≤ 0: sin cajas (C_DESP = 0, C_CREC = 0).

## Interpretación

- **C_DESP** = Despliegue Inicial 40% — cajas instaladas en la primera tanda.
- **C_CREC** = Crecimiento Futuro 100% — cajas reservadas para expansión futura.
- La suma máxima por columna es 8 (4+4 en el último rango).

## Distribución entre postes del segmento

Ver `CalcularCtosCommand.cs` (clase `CtoDistributor`):

1. Calcular `(C_DESP, C_CREC)` por lookup.
2. Construir secuencia intercalada: `D, C, D, C, ...` (ej.: D=3,C=1 →
   `D,C,D,D`).
3. Rankear postes PRIORIDAD del segmento por cercanía al midpoint del
   segmento (centrales primero).
4. Round-robin de la secuencia sobre los postes.
5. Si hay más cajas que postes, los primeros postes reciben más de una.
