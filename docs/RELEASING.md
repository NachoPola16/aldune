# Publicar una versión

## Versiones y tags

- La versión vive en `src/Aldune/Aldune.csproj` (`Version`, `AssemblyVersion`, `FileVersion`) y en la
  línea "La ronda actual se identifica como **vX.Y.Z**" de `docs/ROADMAP.md`.
- Al empujar un tag `v*`, `.github/workflows/release.yml` pasa los tests, compila el portable y el
  instalador (Inno Setup) y crea el GitHub Release con los dos.
- **Dos jobs, con permisos distintos.** `build` (Windows) compila, prueba, firma si procede y sube lo que se va a publicar como artefacto
  `release-files`; solo tiene `contents: read` (y `actions: read`, que SignPath necesita para leer sus artefactos). `publish` (Ubuntu)
  solo descarga ese artefacto y ejecuta `gh release create`; es el único con `contents: write` y no compila ni instala nada de terceros.
  Así un paquete NuGet o de Chocolatey comprometido durante la compilación no tiene a mano un token que pueda escribir en el repositorio.
  Los nombres de los ficheros de la release y `SHA256SUMS.txt` no cambian. Inno Setup se instala con la versión fija y `--require-checksums`
  (Chocolatey se niega si el paquete no trae checksum para una descarga que no sea HTTPS; la de Inno Setup es HTTPS).
- **CI** (`ci.yml`) tiene dos jobs: `build-and-test` (Windows: .NET) y `android` (Ubuntu, JDK 21: `./gradlew test`, que incluye la paridad
  con `tests/vectors/`, y `:app:assembleDebug`). El de Android no usa emulador y no firma nada: el APK de la release se sigue compilando en local.
- **Pre-release**: el tag **tiene que llevar guion** (`v0.99.0-rc1`); solo así se publica como
  pre-release. GitHub no lo ofrece como última versión y el aviso de actualizaciones de la app lo
  ignora. Un tag sin guion (`v0.99.0`) sale como versión normal y se ofrece a todos los usuarios.
- **Una pre-release lleva un número menor que la final** (`0.99.0` en el `.csproj`, tag
  `v0.99.0-rc1`, antes de la `1.0.0`): el aviso de actualizaciones compara números, así que quien
  instaló la pre-release recibe el aviso de la final.

Pasos:

```powershell
# 1. Número de versión en Aldune.csproj y ROADMAP.md, tests en verde
dotnet test Aldune.slnx -c Release
# 2. Commit y push a main
git push origin main
# 3. Tag y seguir el workflow hasta que termine en verde
#    pre-release: git tag v0.99.0-rc1   (con guion)
#    final:       git tag v1.0.0
git tag v1.0.0
git push origin v1.0.0
gh run watch
```

La release de Windows lleva cuatro ficheros: el instalador, el portable x64, el portable ARM64 y `SHA256SUMS.txt` (lo genera el paso
`Generate checksums` del workflow, después de firmar si la firma está activa, con el SHA-256 de los otros tres). Si cambia el nombre o el
número de ficheros de la release, hay que actualizar la lista de «Instalar» de `README.md` y `README.en.md`. Un tag de Windows con guion
(`-rc`) también los publica: borrarlo después con `gh release delete <tag> --cleanup-tag`.

Después de cada release hay que actualizar también el servidor de sincronización propio (ver
`scripts/update-sync-server.sh` y `docs/SYNC.md`, "Actualizar el servidor desde Git").

## Android: APK firmado

Android **no tiene workflow**: el APK se compila y se firma en local y se sube a mano a un GitHub Release. El tag es
`android-v0.1.0-rcN` (con guion: sale como pre-release) y no dispara el workflow de Windows, que solo mira `v*`.

1. Versión en `android/app/build.gradle.kts`: sube `versionCode` en 1 y cambia `versionName` (`0.1.0-rc10`). Commit y push
   a `main` (el APK sale de ese árbol: no compiles con cambios sin subir).
2. Tests: `./gradlew test` desde `android/` (JDK 21; ver `CONTRIBUTING.md`).
3. Compilar y firmar, en PowerShell, desde `android/`. El almacén de claves (`.jks`) **no está en el repositorio** y su
   contraseña tampoco: van en tu gestor de contraseñas. La contraseña se pide con `Read-Host`, así no queda escrita en ningún
   fichero ni en el historial:

   ```powershell
   cd <repo>\android
   $env:JAVA_HOME = "C:\Program Files\Eclipse Adoptium\jdk-21.0.12.101-hotspot"
   $env:ALDUNE_KEYSTORE = "<ruta real del almacén>.jks"      # no es el debug.keystore de ~\.android
   $pw = Read-Host "Contraseña de firma de Aldune" -AsSecureString
   $env:ALDUNE_KEYSTORE_PASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($pw))
   # Solo si no son los de siempre: $env:ALDUNE_KEY_ALIAS (por defecto «aldune») y $env:ALDUNE_KEY_PASSWORD (por defecto, la del almacén)
   ./gradlew :app:assembleRelease
   Copy-Item app\build\outputs\apk\release\app-release.apk .\aldune-0.1.0-rcN.apk
   Remove-Item Env:ALDUNE_KEYSTORE_PASSWORD, Env:ALDUNE_KEYSTORE
   ```

   Sin esas variables el build sale **sin firmar** (`app-release-unsigned.apk`): es lo que necesitan F-Droid y las
   compilaciones de comprobación, y no se puede instalar.
4. Verificar antes de publicar:

   ```powershell
   (Get-FileHash .\aldune-0.1.0-rcN.apk -Algorithm SHA256).Hash.ToLower()
   & "$env:LOCALAPPDATA\Android\Sdk\build-tools\36.0.0\apksigner.bat" verify --verbose --print-certs .\aldune-0.1.0-rcN.apk
   ```

   Tiene que decir `Verifies`, un solo firmante y la **huella SHA-256 del certificado de siempre**
   (`a10d33e76b3e3bc82333f44815824d54e6f99f6e7b2183cacae212c9dd348d37`). Si es otra, se firmó con otra clave y Android no
   instalará el APK encima de la versión anterior. `aapt2 dump badging` confirma `versionCode` y `versionName`.
5. Publicar (es una acción hacia fuera: confirmarla): tag y pre-release con el APK adjunto y, en las notas, el SHA-256 del APK y
   la huella del certificado, para que quien lo descargue pueda comprobarlos:

   ```powershell
   gh release create android-v0.1.0-rcN .\aldune-0.1.0-rcN.apk --target main --prerelease --title "Aldune para Android 0.1.0-rcN" --notes-file notas.md
   ```

**Versión estable de Android (`0.1.0`, sin `-rcN`).** Se publica igual, pero con tag `android-v0.1.0`, **sin** `--prerelease` y con
`--latest=false`, y **después** de la de Windows que toque: así «la última» (`releases/latest`) sigue siendo la de Windows y los
Windows siguen avisando. Comprobar al acabar con `gh release list` que la fila «Latest» es la `v1.7.x` y no la de Android:

```powershell
gh release create android-v0.1.0 .ldune-0.1.0.apk --target main --latest=false --title "Aldune para Android 0.1.0" --notes-file notas.md
```

**Por qué la de Android sale como pre-release.** La app de Windows busca actualizaciones en `releases/latest`
(`ReleaseUpdateChecker`), que devuelve la última versión que **no** es pre-release. Si un release de Android (tag
`android-v…`) lo fuera, pasaría a ser «la última» y los Windows dejarían de avisar de las versiones nuevas hasta el siguiente
release de Windows. Mientras la versión sea `0.1.0-rcN`, además, «pre-release» es lo honesto. Cuando Android pase a una versión
estable hay dos salidas: publicarla con `gh release create … --latest=false` (no se marca como última) o hacer que el
comprobador mire la lista de releases y solo los tags `v*` (eso exige una versión de Windows).

Trampas: la ruta del almacén tiene que ser la real (un `C:\RUTA\...` de ejemplo falla en `validateSigningRelease`); el APK se
deja fuera de git (`.gitignore` ignora `android/*.apk`); y una versión de solo Android no necesita actualizar el servidor de
sincronización, que solo cambia con las de Windows y el servidor.

### Verificación de dependencias (Android)

`android/gradle/verification-metadata.xml` fija el SHA-256 de cada dependencia de Gradle: si un artefacto cambia en origen, la compilación
falla en vez de meterlo en el APK. Al **añadir o subir una dependencia** (o el plugin de Android/Kotlin) hay que regenerarlo desde `android/`:

```powershell
./gradlew --write-verification-metadata sha256 test :app:assembleDebug :app:assembleRelease :app:lintDebug
git checkout ../tests/vectors/kotlin-output.json   # el test regenera los nonces
```

Y revisar el diff del XML (solo deben aparecer los componentes nuevos). En .NET no hay equivalente fiable: CI falla si alguna dependencia
tiene una vulnerabilidad conocida (`dotnet list package --vulnerable --include-transitive`); los lockfiles de NuGet se probaron y no
se cumplían en modo bloqueado.

## Firma del ejecutable con SignPath

> **Estado (2026-09-28)**: la solicitud a SignPath Foundation fue **rechazada** por falta de
> visibilidad pública del proyecto (estrellas, forks, referencias externas). Se puede volver a pedir
> más adelante; hasta entonces las versiones salen sin firmar. Al volver a pedirla, restaurar la
> sección "Code signing policy" de los README (está en el historial de git, antes del 2026-09-28).

Sin firma, Windows SmartScreen avisa de "editor desconocido" al descargar el instalador. SignPath
Foundation firma gratis proyectos de código abierto, siempre que se compilen desde el repositorio
público en GitHub Actions (así pueden comprobar que lo firmado sale del código publicado).

El workflow ya está preparado y **se activa solo** cuando existen **todas** estas variables y el
secreto del repositorio (GitHub → Settings → Secrets and variables → Actions). Si falta alguna,
publica sin firmar.

| Tipo | Nombre | Valor |
|---|---|---|
| Secreto | `SIGNPATH_API_TOKEN` | Token de API de un usuario CI de SignPath |
| Variable | `SIGNPATH_ORGANIZATION_ID` | Id de la organización en SignPath |
| Variable | `SIGNPATH_PROJECT_SLUG` | Slug del proyecto (por ejemplo `aldune`) |
| Variable | `SIGNPATH_POLICY_SLUG` | `release-signing` (o `test-signing` para probar) |
| Variable | `SIGNPATH_EXE_CONFIGURATION_SLUG` | Configuración de artefacto para `aldune.exe` |
| Variable | `SIGNPATH_SETUP_CONFIGURATION_SLUG` | Configuración de artefacto para el instalador |

Cómo conseguirlos:

1. Solicitar la firma gratuita en <https://signpath.org/apply> con el repositorio público y la licencia
   (GPL-3.0). Revisar antes sus condiciones actuales (por ejemplo, qué piden si la app se vende en
   otro canal). Lo que exigen al repositorio ya está: la sección "Code signing policy" de los README
   (atribución, roles y privacidad), la opción de desactivar la consulta de versiones nuevas y el
   nombre y la versión en los metadatos del `.exe` y del instalador. Si cambian los roles (otra
   persona con permiso de commit o de aprobar firmas), hay que actualizar esa sección. Piden
   verificación en dos pasos en GitHub y en SignPath.
2. Una vez aceptado, en SignPath: crear el proyecto, conectarlo al repositorio de GitHub como
   *Trusted Build System*, crear un usuario CI y su token, y las dos configuraciones de artefacto
   (un `.exe` suelto para `aldune.exe` y otro para el instalador de Inno Setup).
3. Poner en GitHub los valores de la tabla. El siguiente tag ya sale firmado.
