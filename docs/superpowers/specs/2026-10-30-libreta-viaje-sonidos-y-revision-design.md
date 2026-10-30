# Libreta de viaje, sonidos y revisión de diseño (Android y Windows)

Fecha: 2026-10-30. Estado: aprobado por Nacho en chat («Sí, adelante con todo»). Se construye en cuatro fases, en este orden.

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

Windows **ya tiene** un sistema de sonidos (`SoundPlan`, `AppSounds`): interruptor general apagado de fábrica, elección por evento entre sonidos del sistema y un `.wav` propio, y los eventos Recordatorio, Sync hecha, Sync fallida, Nota creada y Nota archivada. Se **amplía**, no se sustituye, y Android adopta el mismo modelo.

- **Eventos nuevos**: Abrir nota, Cerrar nota, Marcar casilla, Borrar y **Escribir**. Se suman a los cinco existentes con sus claves estables (`noteOpened`, `noteClosed`, `taskChecked`, `noteDeleted`, `typing`).
- **Sonidos propios de la app**, en papel y pluma: pasar hoja, papel que se pliega, toque de pluma, papel que se arruga y rasguño corto de pluma. Se generan por código (`scripts/sounds/generar-sonidos.py`: ruido filtrado y envolventes), así que no hay licencias ni ficheros de terceros. Salen WAV pequeños, incrustados en Windows y como recursos en Android.
- **Nuevas opciones en `SoundChoice`** (se añaden al final: el enum se guarda como número) para elegirlos por evento. Un botón «Papel y pluma para todo» los asigna de una vez. Los sonidos del sistema y el `.wav` propio siguen disponibles.
- **Apagado de fábrica**, como ahora. Los eventos nuevos arrancan en «ninguno» aunque el interruptor general esté encendido; no suenan solos. Además, un volumen de la app (mismo nombre en las dos).
- **Escribir** suena como mucho una vez cada 70 ms y solo con caracteres imprimibles (no con borrar ni con teclas de control).
- **Android**: mismos eventos, mismas claves y mismo interruptor general en Ajustes. Respeta el modo silencio y vibración del sistema y usa el canal de sonidos de interfaz. Hoy Android no tiene sonidos propios (los recordatorios usan la notificación del sistema); los sonidos de la app son aparte.
- Funcionan igual en todos los aspectos.

## Fase 4: exportación automática (en Windows y en Android)

Hoy solo hay exportación manual a Markdown (Windows: por nota y desde el gestor; Android: «Exportar todas las notas» en un zip y por nota). Se añade una
**copia automática en una carpeta que elige el usuario**, en las dos apps: el usuario la quiere para tener un respaldo legible aunque falle la sync o la
base de datos, o para apuntarla a una carpeta que ya sincroniza otra herramienta.

Decisiones del constructor (a confirmar al probar):
- **Apagada de fábrica.** Un ajuste «Copia automática en una carpeta» con «Elegir carpeta…», mismo nombre y mismo comportamiento en las dos.
- **Qué escribe:** un `.md` por nota activa (y archivada, con una casilla), con el nombre del título y las casillas como en la exportación manual. Las notas
  **protegidas se dejan fuera**, como en la manual: sus textos no salen en claro sin permiso.
- **Cuándo:** Windows, unos segundos después de cada cambio (con pausa para agrupar) y al cerrar; Android, tras un cambio y como mínimo una vez al día
  (WorkManager). Siempre en segundo plano y sin bloquear la edición.
- **Nunca borra nada del usuario.** Solo crea y reescribe sus propios ficheros, que reconoce por un manifiesto `.aldune-export.json` en la carpeta. Una nota
  borrada o a la papelera se mueve a una subcarpeta `_eliminadas/`; nunca se borra un fichero (misma regla que los archivos vinculados). Si el usuario cambió
  un fichero a mano desde la última copia, no se pisa: se guarda al lado con un sufijo.
- **Android:** la carpeta se elige con el selector del sistema (`ACTION_OPEN_DOCUMENT_TREE`, permiso persistente); si se pierde el permiso o la carpeta, se
  avisa una vez y se apaga, sin reintentos infinitos.
- **Aviso claro en la interfaz:** estos ficheros están **en claro** fuera de la base cifrada; quien los elige decide dónde ponerlos.
- La lógica de nombres, del manifiesto y de qué escribir va en Core / `text`, con tests; la parte de plataforma solo escribe ficheros.

## No objetivos

- No cambia el formato de sync ni los datos de las notas.
- No hay sonidos de terceros ni voz.
- No se rediseña la navegación de Android (buscador arriba, «+» abajo).

## Restricciones del proyecto que aplican

Textos con `Strings.T` en cinco idiomas (Windows) y `android/strings.tsv` (Android); colores por `AppPalette` y no hex sueltos; compatibilidad de `settings.json` y de las preferencias de Android sin migración; comentarios en español; lógica nueva en Core/`text`/`domain` con test primero; commits con autor y committer de Nacho, sin `Co-Authored-By`.

## Lo que queda al constructor

El orden interno de cada fase, los colores exactos de las paletas (se comprueban con los tests de contraste), los detalles de las miniaturas y de las texturas, y los valores concretos de los sonidos.
