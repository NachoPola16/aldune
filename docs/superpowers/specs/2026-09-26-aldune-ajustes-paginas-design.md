# Aldune — Ajustes con menú lateral y modo simplificado con lo esencial (design spec)

Decidido con el usuario el 2026-09-26 ("haz lo que consideres") tras medir la ventana con una sonda.

## Problema

- Ajustes medía 920 × 2150 px con todo abierto. En una pantalla de 1440 px de alto quedaba cortada a
  1296 y había que desplazarse para casi todo; la columna izquierda era el doble de alta que la derecha.
- El modo simplificado escondía **todos** los ajustes (solo quedaban el botón de modo, la ayuda y la
  versión), aunque su texto dijera "deja lo esencial".

## Decisiones

- **No ensanchar ni depender de la pantalla**: menú lateral con páginas, tamaño fijo de 900 × 700 con
  tope contra el área de trabajo del monitor (90 %). Cada página cabe sin desplazarse en una pantalla
  normal; en una pequeña, cada página tiene su propio scroll.
- **Páginas**: General (modo de interfaz, arranque, atajo, idioma) · Notas y tareas (recordar
  posición, tareas, papelera) · Dock y pantallas (pantalla y borde lado a lado, pantalla completa,
  mantener abierto, trackpad) · Colores · Sincronización · Ayuda · Acerca de (versión, buscar
  actualizaciones, reiniciar, salir).
- **El gestor de notas no entra en Ajustes**: se usa a diario, Ajustes poco. Queda un botón
  "Gestionar notas" al pie del menú lateral (el gestor ya tenía uno hacia Ajustes).
- **Ajustes desde el dock**: ya se llega con el clic derecho de sus botones; no se añade un botón
  visible. El clic derecho en "Sincronizar" abre directamente la página de Sincronización
  (`AppCoordinator.OpenSettings(page)`).
- **Modo simplificado**: General, Dock y pantallas (solo pantalla y borde), Colores (solo el tema),
  Ayuda y Acerca de. Esconde Notas y tareas, Sincronización y lo avanzado del dock y de los colores.
  Los valores por defecto de lo escondido no cambian: ya valen para empezar.
- La última página vista se recuerda durante la sesión.

## Verificación

Sonda con la ventana real en los dos modos: alto de contenido de cada página frente al visible, y
captura de cada una. Mismos nombres de controles y manejadores que antes (comprobado comparando el XAML).
