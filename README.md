<p align="center"><img src="docs/branding/aldune-logo-128.png" alt="Aldune" width="96"></p>

# Aldune

**Notas al borde de tu pantalla.** Aldune es una app de notas adhesivas para Windows: tus notas
viven como un mazo de pestañas en un borde de la pantalla, se despliegan en abanico al pasar el
ratón y se abren como ventanas propias. Sin cuenta, sin nube obligatoria, y con el contenido cifrado.

[English](README.en.md)

![Aldune: dos notas abiertas y el dock de pestañas en el borde derecho](docs/images/aldune.png)

## Qué hace

- **Dock en el borde de la pantalla**, en cualquiera de los cuatro, en todas las pantallas o en una.
  Se oculta solo ante juegos y vídeos a pantalla completa.
- **Tareas y listas** dentro de cada nota (`Ctrl+L`, `Ctrl+Mayús+L`), con sangría, y la opción de
  borrar solas las tareas hechas pasado un tiempo.
- **Recordatorios**, **etiquetas**, **archivo** y **papelera**, y un gestor para buscar y ordenar.
- **Temas de color**: Clásico, Sereno y Grafito, con notas claras, oscuras o alternando, o los tuyos.
- **Atajo global** para crear una nota desde cualquier sitio (por defecto `Ctrl+Mayús+N`).
- **Notas con contraseña**, cifradas con una clave que sale de la contraseña (que no se guarda), con un nombre visible opcional para reconocerlas sin abrirlas.
- **Sincronización opcional** entre dispositivos por carpeta compartida o NAS, por tu propio
  servidor (Docker) o por WebDAV / Nextcloud, cifrada de extremo a extremo. Ver
  [docs/SYNC.md](docs/SYNC.md).
- **Copias de seguridad automáticas**: una al día, se guardan las 7 más recientes.
- **Accesible**: anillo de foco visible al navegar con Tab, colores de nota que se eligen con las flechas, «Tamaño del texto» (100–150 %) y zoom de una
  nota con `Ctrl` + rueda, y alto contraste de Windows. El dock se abre con el ratón; sin ratón, el atajo opcional `Ctrl+Alt+M` (Ajustes → General)
  abre la lista de notas (Intro abre una, Espacio la marca). Más en «Ayuda rápida» de Ajustes.
- En **español**, **inglés**, **alemán**, **francés** y **portugués de Brasil**.

## Instalar

Descarga la última versión desde [Releases](https://github.com/NachoPola16/aldune/releases):

- **Instalador** (`Aldune-Setup-x.y.z.exe`): accesos directos y desinstalador. No necesita permisos
  de administrador. Desinstalar no borra tus notas.
- **Portable** (`aldune-portable-win-x64.zip`): un solo `aldune.exe`, sin instalar nada.
- **Portable para ARM** (`aldune-portable-win-arm64.zip`): lo mismo para Windows en ARM64 (Surface Pro X, portátiles Snapdragon). Los x64 también
  funcionan ahí, emulados; este va nativo.
- **`SHA256SUMS.txt`**: la huella (SHA-256) de cada fichero de esa versión, para comprobar que lo descargado es exactamente lo publicado.

Requisitos: Windows 10 u 11 de 64 bits. No hace falta instalar .NET.

**Comprobar la descarga** (opcional, en PowerShell). El resultado tiene que ser igual a la línea de ese fichero en `SHA256SUMS.txt`:

```powershell
(Get-FileHash .\Aldune-Setup-2.0.0.0.exe -Algorithm SHA256).Hash.ToLower()
```

Sirve para detectar una descarga corrupta o cambiada por el camino. No sustituye a una firma de código (ver «Firma y privacidad»): quien pudiera
cambiar el fichero en GitHub también podría cambiar su huella.

Si Windows muestra "Windows protegió su PC", es porque el ejecutable todavía no está firmado: pulsa
"Más información" y "Ejecutar de todas formas".

## Android

<p align="center">
  <img src="android/fastlane/metadata/android/en-US/images/phoneScreenshots/1_notes.png" alt="Aldune en Android: la lista de notas" width="200">
  <img src="android/fastlane/metadata/android/en-US/images/phoneScreenshots/2_checklist.png" alt="Aldune en Android: una lista de tareas" width="200">
  <img src="android/fastlane/metadata/android/en-US/images/phoneScreenshots/3_reminder.png" alt="Aldune en Android: opciones y recordatorio de una nota" width="200">
</p>

Hay un cliente para Android (Kotlin nativo, Android 8 o superior) en la carpeta [`android/`](android/): notas con
listas de tareas, etiquetas, notas protegidas con contraseña, recordatorios, widget y captura rápida, los mismos aspectos que en Windows (también los retro: XP, Telecomunicaciones, Bash y Fósforo) y la misma
sincronización cifrada de extremo a extremo que en Windows. Instalar: descarga el APK firmado de
[Releases](https://github.com/NachoPola16/aldune/releases) (las versiones `android-v…`; en las notas de cada una van el SHA-256 del APK y la huella del certificado de firma) y ábrelo en el móvil.
Tiene un **modo simplificado** (Ajustes → «Usar modo simplificado») que deja a la vista solo la sincronización, el aspecto y el idioma, y una **lista rápida**: elige una nota como lista y añade artículos desde la notificación de captura sin abrir la app.

**Algo parecido al dock en Android.** El móvil no tiene un borde de pantalla donde pegar pestañas como en Windows, pero hay dos
formas de tener tus notas siempre a mano: los **widgets** (el mazo en 4×2, 2×2 o 4×4, y un botón 1×1 de nota nueva) y, desde
**Android 17**, la **burbuja**: mantén pulsado el icono de Aldune en el lanzador y elige la opción de burbuja. La app flota sobre
cualquier otra, se pliega a un icono que arrastras a un borde de la pantalla y la abres con un toque, sin salir de lo que estabas
haciendo. Es una función del sistema (Aldune no tiene que hacer nada); el nombre exacto de la opción depende del lanzador.

Para sincronizar el móvil con el PC y tu servidor:

1. En Aldune para Windows: Ajustes → Sincronización entre dispositivos → **Compartir perfil**, y copia el código
   (`aldune-profile-v2:…`).
2. Pásalo al móvil (un mensaje a ti mismo, por ejemplo).
3. En la app de Android: Ajustes → Sincronización, pega el código y pulsa **Continuar**.
   **Con QR, sin pasar el código:** en Windows pulsa **Mostrar QR** (junto a «Compartir perfil»; se cierra solo a los 2 minutos y no sale
   en capturas) y en Android pulsa **Escanear QR**. La cámara solo se abre al pulsar el botón y no se guarda nada de lo que ve.
4. Escribe el **token** de tu servidor: no va dentro del código ni del QR, a propósito.

El código (y el QR) contiene la clave de cifrado de tus notas: trátalo como una contraseña y no lo compartas en sitios públicos.
Más detalle en [docs/SYNC.md](docs/SYNC.md).

## Privacidad y seguridad

Tus notas se guardan en `%LOCALAPPDATA%\Aldune`, en tu equipo. El texto de cada nota va cifrado con
AES-256-GCM, y la clave está protegida con tu usuario de Windows. Aldune no tiene cuenta ni servidor
propio. Lo único que sale de tu equipo es la sincronización, si la activas (y solo hacia el
almacén que tú elijas), y una consulta diaria a GitHub para avisarte de versiones nuevas (se puede
desactivar en Ajustes → Acerca de).

La sincronización cifra el contenido antes de que salga del equipo: el almacén no puede leer tus
notas ni fabricar cambios en ellas. El detalle, incluido lo que **no** protege, está en
[docs/SYNC.md → Modelo de seguridad](docs/SYNC.md#modelo-de-seguridad).

## Firma y privacidad

El instalador y el ejecutable **no están firmados** todavía, así que Windows SmartScreen puede avisar
de "editor desconocido" al abrirlos: pulsa "Más información" → "Ejecutar de todas formas". Se compilan
en GitHub Actions desde este repositorio público (`.github/workflows/release.yml`).

Privacidad: este programa no envía información a otros sistemas de la red salvo que lo pida
expresamente quien lo usa o lo instala. Las dos excepciones se activan o se pueden desactivar: la
sincronización, que solo funciona si la configuras y solo habla con el almacén que elijas, y la
consulta diaria de versiones nuevas, que pide a GitHub la lista de versiones, no envía nada tuyo ni
de tus notas y se desactiva en Ajustes → Acerca de.

## Compilar desde el código

Con Windows y el SDK de .NET 10:

```powershell
dotnet build Aldune.slnx -c Release
dotnet test Aldune.slnx -c Release
./scripts/build-installer.ps1   # portable e instalador en dist/ (necesita Inno Setup 6)
```

Cómo se publica una versión y cómo se firma: [docs/RELEASING.md](docs/RELEASING.md).

## Documentación

- [`docs/SYNC.md`](docs/SYNC.md): sincronización, servidor propio y modelo de seguridad.
- [`docs/STATUS.md`](docs/STATUS.md): estado técnico y decisiones de implementación.
- [`docs/ROADMAP.md`](docs/ROADMAP.md): lo previsto y lo descartado, con sus razones.
- [`docs/superpowers/`](docs/superpowers/): especificaciones y planes de trabajo.

## Probar y dar tu opinión

Si la estás probando: Windows [v2.0.0](https://github.com/NachoPola16/aldune/releases/tag/v2.0.0), Android [1.0.0](https://github.com/NachoPola16/aldune/releases/tag/android-v1.0.0).
Un fallo, una duda o una idea van a [Issues](https://github.com/NachoPola16/aldune/issues/new): di qué versión usas, qué hiciste y qué esperabas.
No pegues códigos de perfil, tokens ni el contenido de tus notas en un issue público.

## Licencia

[GPL-3.0](LICENSE). Puedes usar, estudiar, modificar y compartir Aldune; si distribuyes una versión
modificada, tiene que seguir siendo libre bajo la misma licencia.
