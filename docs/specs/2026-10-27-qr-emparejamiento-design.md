# Emparejar por QR (Windows lo muestra, Android lo escanea)

Fecha: 27-10-2026. Estado: **implementada** (el plan de ejecución se retiró del árbol). Sustituye la decisión «No se hace por ahora» de
`docs/DECISIONES-2026-10-26.md`, que se aplazó por la densidad del código; con lo medido abajo no hace falta dividirlo.

## Para qué

Unir un móvil a un perfil de sync sin pegar el código: en el PC se pulsa «Mostrar QR», en el móvil «Escanear QR», y el campo del código
se rellena igual que con «Pegar». El token **nunca** va en el QR (decisión del 26-10): se sigue escribiendo aparte.

## Lo que se mantiene

- El código de perfil es el de siempre (`aldune-profile-v2:…`): el QR es solo otra forma de transportarlo. Cero cambios de formato de sync.
- El móvil sigue sin crear ni compartir perfiles: solo se une.
- Android solo rellena el campo; `parseProfileInput` y «Continuar» validan como hoy.

## Seguridad

- El código lleva la **clave de cifrado** del perfil. Un QR en pantalla tiene el mismo riesgo que copiar el código: quien lo vea o lo fotografíe
  puede leer esas notas. Por eso:
  - La ventana de Windows lo avisa («Enséñalo solo a tus dispositivos») y **se cierra sola a los 2 minutos**, con cuenta atrás visible.
  - No se guarda ni se escribe en disco; no va al portapapeles (el botón «Copiar código» de siempre es aparte y explícito).
  - «Revocar acceso» sigue siendo la salida si alguien lo vio.
- Android: no se guardan fotogramas ni se envían a ningún sitio; la cámara se abre solo en la pantalla del escáner y se libera al salir.
  Solo se acepta un texto que contenga `aldune-profile-v2:` (`ProfileCodes.findShareCode`); cualquier otro QR se ignora con un aviso.
  El permiso de cámara se pide al pulsar «Escanear QR», no al arrancar, y `uses-feature camera` va `required=false`.

## Tamaño

Medido: «todas las notas» ≈ 250 caracteres; con notas elegidas, ≈ 50 más por nota. Un QR de nivel de corrección M admite unos 2.300 bytes, así
que caben cientos de notas. Si aun así no cabe (`DataTooLongException`), Windows dice «Este perfil es demasiado grande para un QR» y deja
copiar el código como hoy. No se divide en varios QR.

## Windows

- Dependencia nueva: **QRCoder** (MIT, C# puro, versión fijada en el `.csproj`). Solo se usa para la matriz de módulos.
- Pintado propio en WPF (rectángulos negros sobre blanco con zona de silencio de 4 módulos): nítido a cualquier tamaño y sin
  `System.Drawing`. Siempre blanco y negro, también en los aspectos oscuros: un QR claro sobre oscuro no lo leen todos los lectores.
- `ShareQrWindow` (modal, mismo estilo que `AppDialog`): el QR, el aviso, la cuenta atrás y «Cerrar». Se abre desde un botón nuevo «Mostrar QR» junto
  a «Compartir perfil» en Ajustes → Sincronización. Textos con `Strings.T` en los cinco idiomas.
- Lógica pura y con test en Core: `ShareQr.Build(code)` → matriz de módulos o «demasiado grande» (el test fija capacidad, matriz estable para un
  código dado y el límite).

## Android

- **CameraX** (cámara actual de Android; no la API antigua Camera1) + **ZXing `core`** (decodificador de Java puro, Apache-2.0; sin Google
  Play Services, sin ML Kit y sin binarios nativos, así que F-Droid e IzzyOnDroid pueden construirlo). Descartados: `zxing-android-embedded`
  (modo mantenimiento y Camera1), Google Code Scanner y ML Kit (propietarios, sin F-Droid) y `zxing-cpp` (nativo; queda como mejora si el
  escaneo en pruebas reales resulta flojo).
- `QrScanScreen` (Compose + `PreviewView`): `ImageAnalysis` con `STRATEGY_KEEP_ONLY_LATEST`, decodifica el plano de luminancia con
  `MultiFormatReader` restringido a QR, y al primer código válido devuelve el texto y cierra.
- Botón «Escanear QR» junto a «Pegar» en «Añadir perfil»; su resultado va por el mismo camino que «Pegar» (`ProfileCodes.findShareCode`).
- Lógica pura y con test en JVM: `QrDecoder.decodeText(luma, rowStride, width, height)` (ZXing core; QR normal, invertido y con stride de CameraX). El filtro de «solo códigos de perfil» ya existe: `ProfileCodes.findShareCode`.

## Fuera de alcance

Generar el QR desde Android, escanear otros tipos de QR, guardar el QR como imagen, y QR de varios trozos.

## Pruebas

- Core y JVM: lo indicado arriba, con test primero.
- A mano: el PC enseña el QR y el Pixel lo escanea (cámara real, sin capturas de su pantalla de inicio); QR demasiado grande; permiso de cámara
  denegado (la pantalla lo explica y ofrece «Pegar»); otro QR cualquiera (se ignora).
- `seguridad` antes de publicar: ventana de Windows, ciclo de vida de la cámara y que el código no quede en logs.
