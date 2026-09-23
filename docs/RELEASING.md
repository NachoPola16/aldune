# Publicar una versión

## Versiones y tags

- La versión vive en `src/Aldune/Aldune.csproj` (`Version`, `AssemblyVersion`, `FileVersion`) y en la
  línea "La ronda actual se identifica como **vX.Y.Z**" de `docs/ROADMAP.md`.
- Al empujar un tag `v*`, `.github/workflows/release.yml` pasa los tests, compila el portable y el
  instalador (Inno Setup) y crea el GitHub Release con los dos.
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

Después de cada release hay que actualizar también el servidor de sincronización propio (ver
`scripts/update-sync-server.sh` y `docs/SYNC.md`, "Actualizar el servidor desde Git").

## Firma del ejecutable con SignPath

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
   otro canal).
2. Una vez aceptado, en SignPath: crear el proyecto, conectarlo al repositorio de GitHub como
   *Trusted Build System*, crear un usuario CI y su token, y las dos configuraciones de artefacto
   (un `.exe` suelto para `aldune.exe` y otro para el instalador de Inno Setup).
3. Poner en GitHub los valores de la tabla. El siguiente tag ya sale firmado.
