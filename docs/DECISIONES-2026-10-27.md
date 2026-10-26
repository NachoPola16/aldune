# Decisiones del 27-10-2026

Tomadas con el usuario antes de la última release de la tanda (Windows 1.7.x y Android 0.1.0). Complementa `DECISIONES-2026-10-26.md`.

## Modo simplificado en Android: hecho

En Windows es un interruptor de la ventana de Ajustes: esconde las páginas de Notas y Sincronización y lo avanzado del dock y de los
colores, y los valores escondidos se quedan como están. En Android hace lo mismo con la pantalla de Ajustes: un botón al final
(«Usar modo simplificado» / «Usar modo completo»), que se aplica al instante, no toca ningún otro ajuste y no se esconde nunca.

| Bloque de Ajustes | Simplificado |
|---|---|
| Sincronización (con «Escanear QR») | se ve |
| Aspecto (la lista de aspectos) | se ve |
| Idioma | se ve |
| Versión | se ve |
| Captura rápida (notificación, lista rápida, widget, mosaico) | oculto |
| Recordatorios (hora exacta, tareas en el aviso) | oculto |
| Colores avanzados del aspecto y «aspecto de las notas» (color uniforme, esquinas, señal de sync) | oculto |
| Notas y tareas (tema de notas, papelera, orden, casillas, ocultar hechas) | oculto |

La regla está en `SettingsSections` (`android/domain`, con test) y la pantalla solo la consulta. **Temas propios** sigue fuera del móvil:
solo se editan en Windows.

## Ajustes predeterminados

Criterio: un valor por defecto tiene que valer para quien no toca nada, no esconder una función ni gastar permisos o batería sin que
se pida. Lo que se revisó y se decidió:

**Android**

| Ajuste | Por defecto | Decisión |
|---|---|---|
| Aspecto | Seguir al sistema | igual |
| Casillas de tarea grandes | **activado en instalaciones nuevas** | cambia: en un móvil táctil es lo cómodo. Quien ya tenía la app conserva lo que veía (apagado) |
| Mover las hechas al final | apagado | igual (como Windows) |
| Ocultar tareas hechas | apagado | igual (no esconder texto sin que se pida) |
| Papelera | 30 días | igual (como Windows) |
| Notificación de captura, widget ampliado, hora exacta de recordatorios | apagados | igual: piden permisos o gastan batería |
| Sync en segundo plano | activado | igual (solo actúa si hay un perfil conectado) |
| Modo simplificado | apagado | igual |

**Windows**: se revisaron todos y se dejan igual: atajos globales y gestos del trackpad activados, ocultar el dock en pantalla completa,
recordar posición y tamaño de las notas, avisos de actualización, vista previa del texto, tareas en el recordatorio; apagados los
sonidos, las casillas grandes (es un ratón), mover y ocultar tareas hechas, y el modo simplificado.

**Ventana de Ajustes de Windows**: se queda con su estructura (General, Notas y tareas, Dock y pantallas, Colores, Sincronización,
Ayuda, Acerca de). Un único cambio: «Mostrar tareas pendientes en el recordatorio» pasa de Dock a «Notas y tareas», que es donde se
busca. La página de Sincronización es densa (seis botones en una fila) pero cada botón es una acción distinta y agruparlos no gana
claridad; se deja.
