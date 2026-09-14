# Aldune

Aldune es una aplicación de notas adhesivas para Windows. Las notas aparecen como un dock en el
borde de la pantalla y se abren como ventanas independientes, con tareas, recordatorios, archivado
y papelera.

## Estado

El proyecto está en desarrollo. La sincronización entre dispositivos está implementada con carpeta
compartida/NAS y con un servidor propio opcional; se puede lanzar manualmente o de forma periódica.
Consulta [docs/SYNC.md](docs/SYNC.md) para configurar Docker Compose.

## Requisitos

- Windows 10/11.
- .NET SDK 10.

## Compilar y probar

```powershell
dotnet restore Aldune.slnx
dotnet build src/Aldune/Aldune.csproj -c Release
dotnet test Aldune.slnx --no-restore
```

## Crear el portable

```powershell
dotnet publish src/Aldune/Aldune.csproj -c Release -r win-x64 --self-contained true `
  /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:DebugSymbols=false /p:DebugType=None -o publish/portable
```

El resultado es `publish/portable/aldune.exe`. Esa carpeta está excluida de Git porque se puede
regenerar y ocupa mucho espacio.

## Distribución para usuarios

El proyecto ofrece dos formatos:

- portable: `aldune.exe` dentro de un ZIP, sin instalación;
- instalador normal: crea accesos directos y un desinstalador, sin borrar las notas de
  `%LOCALAPPDATA%\\Aldune`.

Para crear ambos en Windows, instala Inno Setup 6 y ejecuta:

```powershell
./scripts/build-installer.ps1
```

Los archivos se generan en `dist/`. Las versiones publicadas se preparan automáticamente en GitHub
al crear una etiqueta `v*`, por ejemplo `v0.5.0`.

Para un servidor que ya tenga Nginx y Cloudflare Tunnel, usa
`docker-compose.sync.nginx.yml`; la guía de [sincronización](docs/SYNC.md) incluye la configuración
del proxy y del subdominio HTTPS.

## Datos locales

La aplicación guarda sus datos en `%LOCALAPPDATA%\Aldune`, no junto al ejecutable ni dentro del
repositorio. La base de datos y la clave protegida no deben compartirse ni subirse a GitHub.

## Documentación del proyecto

- [`docs/ROADMAP.md`](docs/ROADMAP.md): funcionalidades previstas y decisiones pendientes.
- [`docs/STATUS.md`](docs/STATUS.md): estado técnico y decisiones de implementación.
- [`docs/superpowers/`](docs/superpowers/): especificaciones y planes de trabajo.

Las notas también pueden protegerse individualmente con contraseña. El contenido se cifra con una
clave derivada de esa contraseña; Aldune no guarda la contraseña y la nota aparece bloqueada en el
dock y en el gestor hasta introducirla. La protección se conserva al sincronizar, pero hay que usar
la misma contraseña en cada dispositivo. Si se pierde, esa nota no se puede recuperar.

## Licencia

La licencia pública todavía no está decidida.
