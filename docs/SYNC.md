# Sincronización

La sincronización no copia `notes.db`. Cada nota se serializa en un sobre JSON y su contenido se
cifra con una clave de sincronización compartida entre dispositivos. El almacén solo ve el id,
la fecha, el dispositivo y el blob cifrado.

## Carpeta compartida

En Ajustes, activa la sincronización, elige `Carpeta compartida / NAS` y selecciona una carpeta local,
UNC (`\\servidor\\fanote`) o una carpeta que ya sincronice Syncthing, OneDrive, Dropbox o Nextcloud.
Genera el código en el primer dispositivo e impórtalo en los demás. La carpeta contiene `objects/`;
no hay que abrir ni editar esos ficheros manualmente.

## Servidor propio con Docker Compose

En el servidor:

```powershell
Copy-Item .env.example .env
# Edita .env y sustituye FANOTE_SYNC_TOKEN por un secreto largo y aleatorio.
docker compose -f docker-compose.sync.yml up -d --build
```

El puerto publicado por defecto es `8087`. Para cambiarlo, pon `FANOTE_SYNC_PORT=9000` en `.env` y
configura en Aldune una URL como `http://192.168.1.20:9000/`. En Aldune selecciona `Servidor propio`,
introduce el mismo token y usa el mismo código de sincronización en todos los dispositivos. El token
debe ser solo el valor de la derecha de `FANOTE_SYNC_TOKEN=`. El botón `Sincronizar ahora` está
disponible en Ajustes y en el dock; también puedes activar la sincronización periódica y elegir el
intervalo. Ajustes muestra la última sincronización correcta.

En Ajustes también puedes elegir entre `Todas las notas` y `Notas seleccionadas`. En el segundo modo,
solo las notas marcadas en `Elegir notas…` se publican o se descargan en ese vínculo. La selección se
guarda localmente y no se envía al servidor.

El servidor es un almacén de blobs y no necesita SQLite. El volumen `fanote-sync-data` contiene los
objetos y sobrevive a actualizaciones del contenedor.

La versión inicial está pensada para una red privada o detrás de una VPN. No se debe publicar el
puerto HTTP directamente en Internet: para acceso externo hay que poner HTTPS delante (proxy inverso,
VPN o túnel seguro). La protección TLS y la rotación de tokens quedan en el roadmap.

## WebDAV, Nextcloud y ownCloud

En Ajustes puedes elegir `WebDAV / Nextcloud` para usar una carpeta WebDAV propia o la de
Nextcloud/ownCloud. Introduce la URL de una carpeta que ya exista, el usuario y, preferiblemente,
una contraseÃ±a de aplicaciÃ³n. Fanote crea dentro de ella la carpeta `objects/` y guarda allÃ­ los
sobres cifrados por nota. La contraseÃ±a se protege localmente en Windows y nunca entra en el cÃ³digo
de invitaciÃ³n ni se sube como parte de los datos.

Para acceso fuera de la red usa siempre una URL `https://`. En una instalaciÃ³n de Nextcloud, la URL
suele tener esta forma:

```text
https://cloud.example.com/remote.php/dav/files/usuario/Aldune/
```

La invitaciÃ³n de perfil puede transportar la URL de WebDAV, pero cada dispositivo debe introducir
su propia cuenta o contraseÃ±a de aplicaciÃ³n. La carpeta WebDAV debe permitir `MKCOL`, `PROPFIND`,
`GET`, `PUT` y `DELETE`; la mayorÃ­a de instalaciones de Nextcloud y ownCloud ya lo permiten.

### HTTPS con Caddy

Para exponer el servidor con HTTPS automÃ¡tico mediante un dominio pÃºblico, usa la composiciÃ³n
preparada en `docker-compose.sync.https.yml`. Requiere que el DNS de `FANOTE_SYNC_DOMAIN` apunte al
servidor y que los puertos 80 y 443 lleguen a Docker:

```powershell
Copy-Item .env.example .env
# AÃ±ade tambiÃ©n al .env:
# FANOTE_SYNC_DOMAIN=sync.example.com
docker compose -f docker-compose.sync.https.yml up -d --build
```

Caddy obtiene y renueva el certificado, y solo publica Caddy hacia Internet; `fanote-sync` queda en
la red interna de Docker. En Aldune configura `https://sync.example.com/`. Los datos de Caddy viven
en los volÃºmenes `caddy-data` y `caddy-config`, separados de los sobres cifrados de Fanote.

Si el servidor solo se accede por VPN o red local, conserva `docker-compose.sync.yml` y usa HTTP dentro
de esa red. No desactives la verificaciÃ³n TLS en el cliente para evitar errores de certificado.

La rotaciÃ³n de tokens y la interfaz de conflictos ya estÃ¡n implementadas. Para HTTPS real falta
probar la composiciÃ³n con un dominio y DNS controlados por el usuario.

### Rotación de tokens

El servidor sigue aceptando el token único `FANOTE_SYNC_TOKEN`. Para rotarlo sin cortar los
dispositivos, usa temporalmente la lista `FANOTE_SYNC_TOKENS` en `.env`:

```dotenv
FANOTE_SYNC_TOKENS=nuevo-token,token-antiguo
```

Recrea el contenedor, cambia el token en todos los dispositivos y comprueba que sincronizan. Cuando
todos usen el nuevo, deja solo `FANOTE_SYNC_TOKENS=nuevo-token` y recrea el contenedor otra vez: el
antiguo queda revocado. La lista tiene prioridad sobre `FANOTE_SYNC_TOKEN`; nunca pongas tokens en
la URL ni en el repositorio.

### Perfiles de sincronizaciÃ³n

Puedes crear varios perfiles desde Ajustes. Cada perfil mantiene su transporte, servidor o carpeta,
token, clave de sincronizaciÃ³n, notas seleccionadas y Ãºltima sincronizaciÃ³n por separado. La
configuraciÃ³n antigua se migra al perfil `Mis dispositivos` al abrirla por primera vez. Para
compartir una nota con otra persona, crea otro perfil, selecciona solo esa nota y comparte el
cÃ³digo de ese perfil; el flujo futuro de invitaciÃ³n y revocaciÃ³n de perfiles queda pendiente.

### Compartir una selecciÃ³n de notas

En el perfil de origen, crea o selecciona un perfil, marca `Notas seleccionadas` y elige las notas.
`Compartir perfil` copia una invitaciÃ³n que contiene la clave, los identificadores de esa selecciÃ³n,
el nombre del perfil y, si usa un servidor propio, su URL. En el otro dispositivo, crea el perfil,
pega el cÃ³digo y pulsa `Importar cÃ³digo`; el transporte y la URL del servidor se rellenan solos,
pero el token siempre debe introducirse aparte. El token nunca viaja dentro del cÃ³digo. Las
invitaciones antiguas `fanote-profile-v1` siguen siendo compatibles.

## Compatibilidad futura

La compatibilidad se controla por el formato de sincronizaciÃ³n, no por el nÃºmero visible de Fanote.
Las actualizaciones que mantengan el mismo formato pueden sincronizarse entre sÃ­. Si una versiÃ³n
introduce un cambio incompatible, aumenta el formato y cada cliente acepta solo la ventana que sabe
leer (`MinimumSupportedFormat`-`CurrentFormat`). Una versiÃ³n demasiado antigua o futura devuelve
un error claro y no aplica datos parcialmente.

El formato de los sobres y la API son JSON/HTTP y no dependen de Windows, WPF, SQLite ni DPAPI. Un
futuro cliente para Android, iOS, macOS o Linux podrá reutilizar el servidor. DPAPI solo protege la
clave y el token guardados localmente en la versión Windows.

La versión de formato 2 sincroniza también las etiquetas dentro del contenido cifrado. El formato 1
sigue siendo legible, pero una versión antigua de Fanote rechazará sobres de formato 2 para no
reescribir una nota nueva y perder sus etiquetas sin avisar. El contenido, color, estado, fecha de
creación y eliminaciones permanentes siguen sincronizándose; las posiciones de las ventanas se
quedan locales a cada dispositivo, porque un mismo escritorio no tiene sentido en monitores distintos.

La selección puede ser distinta en cada perfil. Para compartir una nota con otra persona, crea un
perfil separado, selecciona solo esa nota y comparte el código de ese perfil.

### Revocar una invitación

En Ajustes, pulsa `Revocar códigos anteriores` dentro del perfil que quieras proteger. Fanote
sincroniza primero la versión actual, genera una clave nueva y vuelve a cifrar los sobres del vínculo.
Los dispositivos que conserven un código antiguo dejarán de sincronizar; genera después una nueva
invitación y compártela solo con quienes deban continuar. El token del servidor no cambia. Si la
aplicación se cierra durante el proceso, la siguiente sincronización intenta terminar la rotación.

La revocación impide descifrar futuras versiones del almacén, pero no puede borrar una copia que otro
dispositivo ya hubiera descargado antes de revocar.
