# Sincronización

La sincronización no copia `notes.db`. Cada nota se serializa en un sobre JSON y su contenido se
cifra con una clave de sincronización compartida entre dispositivos. El almacén solo ve el id,
la fecha, el dispositivo y el blob cifrado.

## Carpeta compartida

En Ajustes, activa la sincronización, elige `Carpeta compartida / NAS` y selecciona una carpeta local,
UNC (`\\servidor\\aldune`) o una carpeta que ya sincronice Syncthing, OneDrive, Dropbox o Nextcloud.
Genera el código en el primer dispositivo e impórtalo en los demás. La carpeta contiene `objects/`;
no hay que abrir ni editar esos ficheros manualmente.

## Servidor propio con Docker Compose

En el servidor:

```powershell
Copy-Item .env.example .env
# Edita .env y sustituye ALDUNE_SYNC_TOKEN por un secreto largo y aleatorio.
docker compose -f docker-compose.sync.yml up -d --build
```

El puerto publicado por defecto es `8087`. Para cambiarlo, pon `ALDUNE_SYNC_PORT=9000` en `.env` y
configura en Aldune una URL como `http://192.168.1.20:9000/`. En Aldune selecciona `Servidor propio`,
introduce el mismo token y usa el mismo código de sincronización en todos los dispositivos. El token
debe ser solo el valor de la derecha de `ALDUNE_SYNC_TOKEN=`. El botón `Sincronizar ahora` está
disponible en Ajustes y en el dock; también puedes activar la sincronización periódica y elegir el
intervalo. Ajustes muestra la última sincronización correcta.

Los nombres de las variables de entorno son los de `ALDUNE_*`. Hasta el 2026-09-15 se llamaban
`FANOTE_*` y ya no se aceptan: si tu `.env` todavía usa `FANOTE_SYNC_TOKEN`, `FANOTE_SYNC_TOKENS`,
`FANOTE_SYNC_PORT` o `FANOTE_SYNC_DOMAIN`, renómbralos. Sin token el servidor se niega a arrancar,
así que el fallo es inmediato y visible, no silencioso.

En Ajustes también puedes elegir entre `Todas las notas` y `Notas seleccionadas`. En el segundo modo,
solo las notas marcadas en `Elegir notas…` se publican o se descargan en ese vínculo. La selección se
guarda localmente y no se envía al servidor.

El servidor es un almacén de blobs y no necesita SQLite. El volumen lógico `aldune-sync-data` contiene
los objetos y sobrevive a actualizaciones del contenedor. Su nombre físico se configura con
`ALDUNE_SYNC_VOLUME` (por defecto `aldune-sync-data`).

### Migrar un servidor que ya sincronizaba

Hasta el 2026-09-15 la aplicación se llamaba Fanote: el servidor guardaba sus objetos en un volumen
Docker llamado `fanote-sync-data` y leía variables de entorno `FANOTE_*`.

- **Volumen**: si tu servidor ya tiene objetos sincronizados en ese volumen, añade a `.env`
  `ALDUNE_SYNC_VOLUME=fanote-sync-data` para que el contenedor siga montando el mismo volumen.
- **Variables de entorno**: renombra las `FANOTE_*` a `ALDUNE_*`.

Si no lo haces, el contenedor arranca con un volumen vacío. No se pierde nada: los objetos antiguos
siguen en el disco (el volumen anterior no se borra) y cada dispositivo vuelve a subir sus notas al
sincronizar. Lo que sí se pierde es el historial de conflictos y las marcas de borrado del almacén,
así que una nota que se hubiera borrado en un dispositivo podría reaparecer desde otro.

La versión inicial está pensada para una red privada o detrás de una VPN. No se debe publicar el
puerto HTTP directamente en Internet: para acceso externo hay que poner HTTPS delante (proxy inverso,
VPN o túnel seguro). La protección TLS y la rotación de tokens quedan en el roadmap.

## WebDAV, Nextcloud y ownCloud

En Ajustes puedes elegir `WebDAV / Nextcloud` para usar una carpeta WebDAV propia o la de
Nextcloud/ownCloud. Introduce la URL de una carpeta que ya exista, el usuario y, preferiblemente,
una contraseña de aplicación. Aldune crea dentro de ella la carpeta `objects/` y guarda allí los
sobres cifrados por nota. La contraseña se protege localmente en Windows y nunca entra en el código
de invitación ni se sube como parte de los datos.

Para acceso fuera de la red usa siempre una URL `https://`. En una instalación de Nextcloud, la URL
suele tener esta forma:

```text
https://cloud.example.com/remote.php/dav/files/usuario/Aldune/
```

La invitación de perfil puede transportar la URL de WebDAV, pero cada dispositivo debe introducir
su propia cuenta o contraseña de aplicación. La carpeta WebDAV debe permitir `MKCOL`, `PROPFIND`,
`GET`, `PUT` y `DELETE`; la mayoría de instalaciones de Nextcloud y ownCloud ya lo permiten.

### Nginx + Cloudflare Tunnel

Si el servidor ya usa Nginx y Cloudflare Tunnel, esta es la variante recomendada. No ejecutes
tambien la composicion con Caddy: ambos intentarian ocupar los mismos puertos de proxy.

Usa `docker-compose.sync.nginx.yml`, que publica el servidor solo en `127.0.0.1`:

```powershell
Copy-Item .env.example .env
# Edita .env y define un secreto largo:
# ALDUNE_SYNC_TOKEN=un-secreto-largo-y-aleatorio
docker compose -f docker-compose.sync.nginx.yml up -d --build
curl http://127.0.0.1:8097/health
```

En Nginx, crea un host para el subdominio elegido, por ejemplo `sync.example.com`:

```nginx
server {
    listen 80;
    server_name sync.example.com;

    location / {
        proxy_pass http://127.0.0.1:8097;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

En Cloudflare Tunnel añade una ruta `Published application` con este destino local:

```text
Hostname: sync.example.com
Service:  http://127.0.0.1:80
```

Cloudflare termina el HTTPS público y el túnel conecta con Nginx por localhost; no hace falta abrir
el puerto 8097 en el router ni publicarlo en Internet. En Aldune configura
`https://sync.example.com/` y usa el mismo token y código de sincronización de los otros dispositivos.

### Actualizar el servidor desde Git

El servidor no necesita una copia manual de `src/` ni de los ficheros publicados de Windows. El
`Dockerfile` compila dentro de Docker `src/Aldune.SyncServer` y su referencia a `src/Aldune.Core`,
por lo que basta con mantener un checkout del repositorio y reconstruir la imagen. Los objetos
sincronizados permanecen en el volumen Docker y el `.env` debe vivir fuera del checkout para que los
secretos nunca entren en Git.

Configuración inicial recomendada en un servidor Linux:

```bash
sudo mkdir -p /opt/aldune-sync
sudo git clone --depth 1 https://github.com/NachoPola16/aldune.git /opt/aldune-sync
sudo install -m 600 /dev/null /etc/aldune-sync.env
sudo nano /etc/aldune-sync.env
```

En `/etc/aldune-sync.env` define al menos un token, y conserva también cualquier configuración del
servidor existente, por ejemplo:

```dotenv
ALDUNE_SYNC_TOKEN=un-secreto-largo-y-aleatorio
ALDUNE_SYNC_PORT=8097
# Si el servidor ya usaba el almacén antiguo:
# ALDUNE_SYNC_VOLUME=fanote-sync-data
```

Para la variante recomendada con Nginx y Cloudflare Tunnel, ejecuta la primera actualización así:

```bash
sudo env ALDUNE_ENV_FILE=/etc/aldune-sync.env \
  bash /opt/aldune-sync/scripts/update-sync-server.sh
```

En las siguientes versiones solo hace falta repetir ese comando. El script hace `git pull --ff-only`
de `main`, reconstruye `docker-compose.sync.nginx.yml` con `--build`, conserva el volumen de datos y
comprueba `/health`. No copia `src/`, no modifica el `.env` y falla antes de actualizar si falta el
fichero de secretos.

Si se usa otra composición, se puede seleccionar sin editar el script:

```bash
sudo env ALDUNE_ENV_FILE=/etc/aldune-sync.env \
  ALDUNE_COMPOSE_FILE=docker-compose.sync.https.yml \
  ALDUNE_HEALTH_URL=http://127.0.0.1:8087/health \
  bash /opt/aldune-sync/scripts/update-sync-server.sh
```

Para desplegar desde otro directorio se puede usar `ALDUNE_REPO_DIR`; para una rama distinta,
`ALDUNE_BRANCH`. El repositorio público no requiere credenciales para el `git pull`; si se hace
privado, configura una deploy key de solo lectura en el servidor, nunca un token dentro de este
repositorio ni del fichero de sincronización.

Si gestionas el túnel mediante `config.yml`, la regla equivalente es:

```yaml
ingress:
  - hostname: sync.example.com
    service: http://127.0.0.1:80
  - service: http_status:404
```

Valida la configuración con `cloudflared tunnel ingress validate` y crea el DNS con
`cloudflared tunnel route dns <TUNNEL> sync.example.com`, si no lo has creado desde el panel.

### HTTPS con Caddy

Para exponer el servidor con HTTPS automático mediante un dominio público, usa la composición
preparada en `docker-compose.sync.https.yml`. Requiere que el DNS de `ALDUNE_SYNC_DOMAIN` apunte al
servidor y que los puertos 80 y 443 lleguen a Docker:

```powershell
Copy-Item .env.example .env
# Añade también al .env:
# ALDUNE_SYNC_DOMAIN=sync.example.com
docker compose -f docker-compose.sync.https.yml up -d --build
```

Caddy obtiene y renueva el certificado, y solo publica Caddy hacia Internet; `aldune-sync` queda en
la red interna de Docker. En Aldune configura `https://sync.example.com/`. Los datos de Caddy viven
en los volúmenes `caddy-data` y `caddy-config`, separados de los sobres cifrados de Aldune.

Si el servidor solo se accede por VPN o red local, conserva `docker-compose.sync.yml` y usa HTTP dentro
de esa red. No desactives la verificación TLS en el cliente para evitar errores de certificado.

La rotación de tokens y la interfaz de conflictos ya están implementadas. Para HTTPS real falta
probar la composición con un dominio y DNS controlados por el usuario.

### Rotación de tokens

El servidor sigue aceptando el token único `ALDUNE_SYNC_TOKEN`. Para rotarlo sin cortar los
dispositivos, usa temporalmente la lista `ALDUNE_SYNC_TOKENS` en `.env`:

```dotenv
ALDUNE_SYNC_TOKENS=nuevo-token,token-antiguo
```

Recrea el contenedor, cambia el token en todos los dispositivos y comprueba que sincronizan. Cuando
todos usen el nuevo, deja solo `ALDUNE_SYNC_TOKENS=nuevo-token` y recrea el contenedor otra vez: el
antiguo queda revocado. La lista tiene prioridad sobre `ALDUNE_SYNC_TOKEN`; nunca pongas tokens en
la URL ni en el repositorio.

### Perfiles de sincronización

Puedes crear varios perfiles desde Ajustes. Cada perfil mantiene su transporte, servidor o carpeta,
token, clave de sincronización, notas seleccionadas y última sincronización por separado. La
configuración antigua se migra al perfil `Mis dispositivos` al abrirla por primera vez. Para
compartir una nota con otra persona, crea otro perfil, selecciona solo esa nota y comparte el
código de ese perfil; el flujo futuro de invitación y revocación de perfiles queda pendiente.

### Compartir una selección de notas

En el perfil de origen, crea o selecciona un perfil, marca `Notas seleccionadas` y elige las notas.
`Compartir perfil` copia una invitación que contiene la clave, los identificadores de esa selección,
el nombre del perfil y, si usa un servidor propio, su URL. En el otro dispositivo, crea el perfil,
pega el código y pulsa `Importar código`; el transporte y la URL del servidor se rellenan solos,
pero el token siempre debe introducirse aparte. El token nunca viaja dentro del código. Las
invitaciones antiguas (`fanote-profile-v1` y `fanote-profile-v2`) siguen siendo compatibles; los
nuevos se generan con el prefijo de la marca vigente (`aldune-profile-v2`).

## Compatibilidad futura

La compatibilidad se controla por el formato de sincronización, no por el número visible de Aldune.
Las actualizaciones que mantengan el mismo formato pueden sincronizarse entre sí. Si una versión
introduce un cambio incompatible, aumenta el formato y cada cliente acepta solo la ventana que sabe
leer (`MinimumSupportedFormat`-`CurrentFormat`). Una versión demasiado antigua o futura devuelve
un error claro y no aplica datos parcialmente.

El formato de los sobres y la API son JSON/HTTP y no dependen de Windows, WPF, SQLite ni DPAPI. Un
futuro cliente para Android, iOS, macOS o Linux podrá reutilizar el servidor. DPAPI solo protege la
clave y el token guardados localmente en la versión Windows.

La versión de formato 2 sincroniza también las etiquetas dentro del contenido cifrado. El formato 3
añade las notas protegidas. La versión de formato 4 añade la posición de cada nota dentro del mazo
(`NoteOrder.Position`), para que todos los dispositivos enseñen el mismo orden de pestañas; las
posiciones de las ventanas siguen siendo locales a cada dispositivo, porque un mismo escritorio no
tiene sentido en monitores distintos. Un cliente antiguo rechaza los sobres que no sabe leer con un
error claro en vez de aplicar datos parcialmente: una versión antigua reescribiría la nota sin la
posición y desharía el reordenado sin avisar.

La selección puede ser distinta en cada perfil. Para compartir una nota con otra persona, crea un
perfil separado, selecciona solo esa nota y comparte el código de ese perfil.

### Revocar una invitación

En Ajustes, pulsa `Revocar códigos anteriores` dentro del perfil que quieras proteger. Aldune
sincroniza primero la versión actual, genera una clave nueva y vuelve a cifrar los sobres del vínculo.
Los dispositivos que conserven un código antiguo dejarán de sincronizar; genera después una nueva
invitación y compártela solo con quienes deban continuar. El token del servidor no cambia. Si la
aplicación se cierra durante el proceso, la siguiente sincronización intenta terminar la rotación.

La revocación impide descifrar futuras versiones del almacén, pero no puede borrar una copia que otro
dispositivo ya hubiera descargado antes de revocar.
