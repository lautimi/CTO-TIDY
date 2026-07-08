# Arquitectura de Agentes — CTO AutoCAD Add-In

> Desde 2026-07-08: sifon pasa a Fable 5 (orquestador) y el resto de los
> agentes se uniforma en Sonnet 5 (workers).

## Diagrama

```
                    ┌───────────────────────────┐
                    │  sifon (Fable 5)          │
                    │  Director / orquestador   │
                    │  descompone tareas        │
                    └─────────────┬─────────────┘
                                  │ delega
        ┌───────────┬────────────┼────────────┬───────────┐
        ▼           ▼            ▼            ▼           
┌──────────────┐ ┌───────────┐ ┌───────────┐ ┌───────────┐
│ paredes      │ │ ander     │ │ roman     │ │ delgado   │
│ Sonnet 5     │ │ Sonnet 5  │ │ Sonnet 5  │ │ Sonnet 5  │
│ Ejecutor     │ │ Doc keeper│ │ Git & Hub │ │ Build/dep │
└──────────────┘ └───────────┘ └───────────┘ └───────────┘
```

## Asignación de modelos

| Agente | Rol | Modelo |
|---|---|---|
| sifon | Director / orquestador | Fable 5 |
| paredes | Ejecutor de código | Sonnet 5 |
| ander | Doc keeper | Sonnet 5 |
| roman | Git & GitHub | Sonnet 5 |
| delgado | Build & deploy | Sonnet 5 |

## Justificación

Uniformar el worker model en Sonnet 5 simplifica la operación del equipo.
Fable 5 concentra el razonamiento arquitectónico y la descomposición de
tareas en el orquestador (sifon).

## Nota histórica

> Antes de 2026-07-08: sifon=Opus, paredes/ander=Sonnet, roman/delgado=Haiku.
> Migración a familia Claude 5 con Fable 5 como orquestador.
