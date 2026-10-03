# Orden de trabajo hacia la 1.5.0, por fases, con modelo y esfuerzo

Para quien retome el trabajo en un chat nuevo. **Al empezar cada fase, di en una línea qué modelo y
esfuerzo toca y espera a que el usuario los cambie** (`/model` y `/effort`) antes de seguir. Si ya
está en los indicados, sigue. Una fase no empieza sin que la anterior esté terminada, probada y
confirmada por el usuario.

Estado de partida (2026-10-03): `main` al día con GitHub; hechos las listas firmes y Ctrl+Alt+L; spec de
aspectos (`docs/superpowers/specs/2026-10-03-aspectos-retro-design.md`) y plan del bloque 1
(`2026-10-03-bloque1-opciones-y-temas.md`) aprobados. Sin publicar.

| Fase | Qué | Modelo | Esfuerzo | Por qué |
|---|---|---|---|---|
| A1 | Bloque 1, tareas 1 a 6 (implementar) | Sonnet 5.5 | medio | El plan trae código, tests y comandos: es aplicarlo con cuidado. La tarea 6 (83 radios) se delega en el agente `mecanico` |
| A2 | Revisión de las tareas 1 y 4 (sync y migración de colores de las notas reales) | Opus 5.5 | alto | Un fallo ahí se propagaría a los otros equipos del usuario |
| A3 | Bloque 1, tarea 7 (sonda de capturas, smoke test, STATUS) | Sonnet 5.5 | medio | Mecánico. Avisar y esperar el "ok" antes del smoke test |
| A4 | Revisión de todo el bloque 1 | Opus 5.5 | alto | Una pasada final independiente |
| B1 | Plan del bloque 2 (piel) | Opus 5.5 | alto | Decisiones de diseño: tipografía, relieve, plantillas WPF |
| B2 | Ejecutar el bloque 2 | Sonnet 5.5 | medio (alto si las plantillas de relieve se atascan) | Igual que A1 |
| C1 | Plan del bloque 3 (seis aspectos) | Opus 5.5 | alto | Paletas derivadas y contraste de colores elegidos por el usuario |
| C2 | Ejecutar el bloque 3 | Sonnet 5.5 | medio | |
| D1 | Plan y ejecución del bloque 4 (exportar/importar) | Sonnet 5.5 | alto | Lógica pura de Core con spec cerrada; la seguridad (nunca sacar credenciales) pide cuidado, no diseño |
| E | Revisión de todo el trabajo de aspectos + publicar 1.5.0 | Opus 5.5 | alto | Publicar es hacia fuera: confirmar con el usuario. Seguir `docs/RELEASING.md` y actualizar el servidor de sync (ver memoria) |
| F1 | Spec de Markdown (escritura, fotos, `.md`, archivos locales) | Opus 5.5 | alto | Cambia el formato de datos para siempre: brainstorming a fondo |
| F2 | Plan del MSIX (spec 2026-09-28) | Sonnet 5.5 | alto | La spec ya está; faltan cuenta de Partner Center y valores de identidad del usuario |

Reglas que valen en todas las fases: TDD en Core, comentarios en español, textos con `Strings.T` en los
cinco idiomas, commits sin `Co-Authored-By`, sondas fuera del repositorio con datos temporales, y las
sondas de teclado y el smoke test mueven ratón y teclado: avisar y esperar el "ok".
