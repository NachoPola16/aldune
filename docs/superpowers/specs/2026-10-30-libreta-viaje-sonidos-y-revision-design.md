# Libreta de viaje, sonidos y revisión de diseño (Android y Windows)

Fecha: 2026-10-30. Estado: aprobado por Nacho en chat («Sí, adelante con todo»). Se construye en tres fases, en este orden.

## Intención

Aldune es una sola app en dos plataformas. Tres cosas a la vez:

1. **Revisión de diseño**: arreglar lo que encontró la crítica de `impeccable` (barra inferior del editor, menús, diálogos, aspectos sin vista previa) en Android, hacer una pasada real a Windows y arreglar lo que salga.
2. **Dos aspectos nuevos con aire de libreta vieja**, en claro y oscuro, en Android y en Windows.
3. **Sonidos de papel y pluma**, opcionales, en las dos apps y en todos los aspectos.

Además, un principio que cruza las tres: **los ajustes de las dos apps están en sintonía**. Mismos nombres, mismo orden, mismos valores por defecto y mismo significado; lo que solo existe en una plataforma (teclado en Android, dock en Windows) se dice en la documentación y no se inventa en la otra.

## Fase 1: revisión

Android:
- Barra de abajo del editor: cinco botones con iconos vectoriales (no glifos de texto): casilla, viñeta `•`, sangría (reducir con pulsación larga), deshacer, rehacer.
- Un solo menú «más»: el `⋮` de arriba. Contiene Opciones de la nota, Subir/Bajar línea, Desmarcar todo y Exportar. Desaparece el `⋯` de abajo y «Quitar hechas» deja de estar duplicado (queda el botón sobre la barra).
- Opciones de la nota: panel inferior con secciones plegables (Color abierto; Etiquetas, Recordatorio y Más plegadas, con su valor actual como resumen).
- Menú al mantener pulsado: panel inferior en vez de diálogo central.
- Aspectos con miniatura de color junto a cada opción.
- Detalles: aviso de copia de seguridad más compacto, botones inactivos con contraste suficiente.
- No se mueve el buscador ni el engranaje abajo: es el patrón Material que el usuario ya conoce; se prefiere familiaridad a novedad.

Windows: pasada con una sonda WPF (`docs/WPF_PROBES.md`, base de datos temporal) sobre el dock, la ventana de nota y Ajustes; se arregla lo que salga (candidatos ya vistos en la captura: botones de la ventana de nota pequeños y de poco contraste; botones del dock sin etiqueta visible).

Sintonía de ajustes: tabla de ajustes de Windows (`AppSettings.cs`) frente a los de Android (`SettingsStore.kt`); se alinean nombre, orden de secciones y valores por defecto, y se documentan los que son de una sola plataforma.

## Fase 2: aspectos

Dos aspectos nuevos que se suman a los que ya hay (Oscuro, Claro, Pastel, Medianoche, XP, Telecom, Bash, Fósforo) a la par, sin jerarquía entre ellos y sin tocar los existentes; cada uno con claro y oscuro (cuatro entradas en `AppPalette`), con colores editables por hueco como los demás aspectos retro (`AspectCatalog`):

- **Libreta de viaje**: papel crema (claro) o marrón de noche (oscuro), tapa de cartón kraft como borde, pliegues, grano de papel, goma elástica y marcapáginas.
- **Cuaderno de campo**: renglones azules y margen rojo, una mancha de café discreta, notas ligeramente torcidas.

Reglas: desgaste **sutil**, siempre por debajo del texto y con poca opacidad; texto legible (los tests de contraste existentes se aplican a las paletas nuevas); título manuscrito y cuerpo en serifa; nada de hex suelto en XAML (claves de `AppPalette`); la textura es un dibujo (vectorial o mosaico pequeño), sin coste apreciable de batería ni de CPU. Quedan fuera por ahora el papel arrugado con borde irregular y la máquina de escribir.

## Fase 3: sonidos

Decisiones tomadas por el constructor (a confirmar al probar):
- Los sonidos **se generan por código** (`scripts/sounds/generar-sonidos.py`, ruido filtrado y envolventes), así que no hay licencias ni ficheros de terceros. Salen WAV pequeños que van incrustados en Windows y como recursos en Android.
- Eventos: abrir nota (pasar hoja), cerrar nota (papel que se pliega), crear nota, marcar casilla (toque de pluma), borrar (papel que se arruga) y **escribir** (rasguño corto de pluma).
- **Apagados por defecto.** Un interruptor «Sonidos», otro aparte «Sonido al escribir», y un volumen. Mismos nombres en las dos apps.
- Escribir suena como mucho una vez cada 70 ms y solo con caracteres imprimibles (no con borrar ni con teclas de control).
- Android respeta el modo silencio y vibración del sistema y usa el canal de sonidos de interfaz; Windows usa solo el volumen propio de la app.
- Funcionan igual en todos los aspectos.

## No objetivos

- No cambia el formato de sync ni los datos de las notas.
- No hay sonidos de terceros ni voz.
- No se rediseña la navegación de Android (buscador arriba, «+» abajo).

## Restricciones del proyecto que aplican

Textos con `Strings.T` en cinco idiomas (Windows) y `android/strings.tsv` (Android); colores por `AppPalette` y no hex sueltos; compatibilidad de `settings.json` y de las preferencias de Android sin migración; comentarios en español; lógica nueva en Core/`text`/`domain` con test primero; commits con autor y committer de Nacho, sin `Co-Authored-By`.

## Lo que queda al constructor

El orden interno de cada fase, los colores exactos de las paletas (se comprueban con los tests de contraste), los detalles de las miniaturas y de las texturas, y los valores concretos de los sonidos.
