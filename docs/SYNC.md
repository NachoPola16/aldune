# Sync

[Español](SYNC.es.md)

Sync does not copy `notes.db`. Each note is serialized into a JSON envelope and its content is
encrypted with a sync key shared between devices. The store only sees the id, the date, the
device and the encrypted blob.

## Choosing how to sync

There are three kinds of store. Notes travel encrypted in all three (see the security model below): what changes is
**where they are kept** and **which devices can use it**.

| Kind | What you need | Windows | Android | When to pick it |
|---|---|---|---|---|
| **Shared folder / NAS** | A folder all your PCs can see (local network, a NAS, or a folder already synced by Syncthing, OneDrive, Dropbox or Nextcloud) | Yes | **No** | You only use PCs and already have a shared folder |
| **Your own server** (Docker) | An always-on machine with Docker (a home server, a NAS, a VPS) | Yes | Yes | You want to control your data and use your phone too |
| **WebDAV / Nextcloud / ownCloud** | A WebDAV account you already have, and an app password | Yes | Yes | You already use Nextcloud or another WebDAV and do not want to set anything up |

**Short rule:** if you are going to use your phone, pick *Your own server* or *WebDAV*. A “shared folder” code does not work on
Android: the app rejects it with “This code is for a shared folder. On the phone use a server or WebDAV”.

All kinds work the same way at first: you set up the first device, **share the profile** (a code that carries the
encryption key) and import it on the others. **The token or password never travels inside the code**: it is typed separately on
each device. Treat the code like a password, because it contains the key to your notes.

### Step by step

The Settings page is called **Sync** (Windows and Android).

#### A. Shared folder (Windows only)

1. Prepare a folder that all your PCs can open: local, on the network (`\\server\aldune`) or one already synced by another tool.
2. On the first PC: Settings → Sync → turn sync on, choose `Shared folder / NAS` and
   select the folder.
3. Click **Share profile** and copy the code.
4. On every other PC: Settings → Sync → click **New** (next to “Sync profile”), paste the code, click `Import code` and pick the folder (it may have a different path
   than on the first PC, but it has to be the same folder).
5. `Sync now` (in Settings and in the dock), or turn on periodic sync.

#### B. Your own server

1. On the server (Linux, or any machine with **Git** and **Docker with Compose v2**; check with `docker compose version`):
   ```bash
   git clone https://github.com/NachoPola16/aldune.git
   cd aldune
   cp .env.example .env
   openssl rand -base64 32      # generates a token; without openssl, use any long random string
   ```
   Open `.env` and replace `replace-with-a-long-random-token` with that value (just the value, no quotes or spaces). Then run
   `docker compose -f docker-compose.sync.yml up -d --build` (the first time it takes a few minutes: it builds the server).
   Check that it answers: `curl http://SERVER-IP:8087/health` must return `{"status":"ok",...}`. If it does not answer from another
   machine but does from the server itself, it is the firewall: open port `8087` (TCP) on the server.
2. On the first PC: Settings → Sync → `Self-hosted server`; URL (`http://SERVER-IP:8087/`) and the
   token. Click **Share profile** and copy the code.
3. On every other PC: Settings → Sync → click **New** (next to “Sync profile”), paste the code, `Import code` and type the token (the transport and the URL fill in by themselves).
4. **On Android:** Settings → Sync → paste the code (or tap **Scan QR** while the PC shows **Show QR**) → **Continue** → choose “Aldune server” → check the address and
   type the token → **Connect**.
5. Away from home use HTTPS (see [Nginx + Cloudflare Tunnel](#nginx--cloudflare-tunnel) and [HTTPS with Caddy](#https-with-caddy)): with
   `http://` the token travels unprotected over the network (the notes are still encrypted), and Android warns you.

#### C. WebDAV, Nextcloud or ownCloud

1. In your service, create a folder for Aldune (for example `Aldune`) and an **app password**. In Nextcloud: Personal
   settings → Security → Devices & sessions → create a new app password. Do not use your main password.
2. The URL of that folder usually looks like `https://cloud.example.com/remote.php/dav/files/USER/Aldune/`.
3. On the first PC: Settings → Sync → `WebDAV / Nextcloud`; URL, user and app password.
   Click **Share profile** and copy the code.
4. On every other PC: Settings → Sync → click **New** (next to “Sync profile”), paste the code, `Import code` and type your user and app password.
5. **On Android:** Settings → Sync → paste the code (or **Scan QR**, as above) → **Continue** → choose “WebDAV” → address (if it is a folder,
   end it with `/`), user and app password → **Connect**.

#### D. A note shared with your family (several profiles)

To share **just one note** (the shopping list, for example) without mixing in the rest of your notes:

1. On Windows, create a separate profile with *Selected notes* and tick only that note. Somebody has to host its store, and it **has to be
   a different one from your personal profile's**: a second Aldune service (another port, another volume and another token; in `docker-compose.sync.yml`
   it is enough to use another project name, container name and volume name) or a WebDAV. The same store with another token does not work: that profile has another
   key and cannot read any of the notes already there, and Aldune refuses to write with “This device's sync code can't read the notes
   in the store”.
2. Click **Share profile** and send the code to each person. The token is given separately, never inside the code.
3. On each person's phone: Settings → Sync → **Add another profile** → paste the code → choose the kind → type the token →
   **Connect**. Android supports several profiles at once, so your personal sync stays as it was.
4. An additional profile **does not ask** whether to upload or discard your notes: it only syncs what its scope includes. If the shared note
   is also part of your “all notes” profile, it is uploaded to both stores.
5. Disconnecting a profile does not touch the others or delete any note.

**Let everyone decide what they share: tag scope.** With *Selected notes* the list of notes is fixed in the code and nobody
can add more from Android. If you want anyone in the group to choose what goes to the family, create the profile with *Notes with a tag* (for
example `family`) and share that code: every note carrying that tag, whoever creates it, syncs with the group, and the ones without
it stay only on their device. Tags travel inside the note's encrypted content (all the tags of that note, not just the
group's one). Removing the tag from a note stops syncing it from that device, but does not delete it from the others: to remove it from
all of them you have to delete it. Changing the scope of an existing profile does not change its key or its token, but the scope travels in the code: phones
that already joined with the previous code have to disconnect the profile and join again with the new code.

Creating and sharing profiles from Android is not possible yet; it is done on Windows.

**Pairing QR.** On Windows, Settings → Sync → **Show QR** shows the profile code as a QR for 2 minutes (black on white, with a warning: it carries the encryption key; it goes neither to the clipboard nor to disk). On Android, **Scan QR** (next to “Paste”) fills in the code field; the token is still typed separately and never travels in the QR. If the profile is too big for a QR (more than ~2,300 bytes), Windows says so and the code is copied as usual.

### What to do if something fails

| What you see | What is happening | What to do |
|---|---|---|
| “The server rejected the credentials” | The token, or the WebDAV user or password, does not match | Check the token (just the value, without `ALDUNE_SYNC_TOKEN=`) or the app password |
| “This profile's key changed: paste the new code” | A new profile with another key was created on another device | Copy the new code and paste it again |
| “No connection” | The server does not answer or there is no network | Check the URL, the port and that the server is on |
| “The profile code is damaged or from a newer version” | The code was copied incompletely, or it is from a newer version | Copy it whole again; update the app if needed |
| “This code is for a shared folder” | The code is for a shared folder | On the phone, create the profile with *Your own server* or *WebDAV* |
| “The address must start with http:// or https://” | The protocol is missing from the address | Write it in full, with `http://` or `https://` |
| New phone and you lost sync | Android's backup does **not** include the notes or the key | Paste the profile code again: the notes come down from the store |

Further below is the detail of each part: security model, server with Nginx, HTTPS, token rotation, profiles and note
selection.

## Security model

What Aldune protects and what it does not, in short:

- **End-to-end encrypted content.** Text, color, tags, dock position and the
  content of password-protected notes travel encrypted with **AES-256-GCM** using the
  sync key, which only your devices have. The store (folder, NAS, server or
  WebDAV) only sees each note's id, its date, the device id and the encrypted block.
- **Integrity.** Each encrypted envelope includes its note's id and date, and they are checked against
  the header: the store cannot swap the content of one note for another's. Since
  1.0 **deletions are signed too**: whoever can write to the store but does not have the
  key cannot forge a deletion. A deletion without a valid signature is ignored: it neither deletes nor sends the note
  to the trash (it used to, and whoever had the token without the key could empty the notes), and it is removed from the
  store so the note gets published again. An object that cannot be decrypted is skipped without stopping the rest of the sync;
  it only stops if **none** can be read, which is what happens with a revoked code.
- **Server access.** Your own server requires a token (`Authorization: Bearer`), which it compares in
  constant time and which can be rotated without downtime (see below). Use a long random value, for
  example `openssl rand -base64 32`; the server warns in its log if it is shorter than 24
  characters.
- **Transport.** The server speaks HTTP; HTTPS is provided by whatever you put in front (Caddy, Nginx +
  Cloudflare Tunnel, a VPN). Aldune warns you in Settings if you configure `http://` to a server
  outside your local network: the notes would still be encrypted, but the token would travel in the clear.
- **On the computer.** The text of each note is stored encrypted with AES-256-GCM in the local database
  (the color, tags, dates and the optional visible name of a protected note are not); the key is protected with
  DPAPI (tied to your Windows user). Password-protected notes use a key derived with
  PBKDF2-SHA256 (600,000 iterations); the password is not stored.
- **What it does not cover.** There is no external audit or certification. Whoever controls the store can
  delete or not serve the files (stop syncing), or serve an old authentic version of a
  note to a device that does not yet have a newer one; they cannot read them or forge changes to them. The metadata (how many notes there are and when they change) is visible to them.

> **When updating to 1.0, update all your devices.** A deletion made from an earlier
> version is not signed: devices on 1.0 ignore it and the note stays where it is.

## Shared folder

In Settings, turn sync on, choose `Shared folder / NAS` and select a local folder,
a UNC one (`\\server\aldune`) or a folder already synced by Syncthing, OneDrive, Dropbox or Nextcloud.
Generate the code on the first device and import it on the others. The folder contains `objects/`;
those files should not be opened or edited by hand.

## Your own server with Docker Compose

On the server:

```bash
cp .env.example .env
# Edit .env and replace ALDUNE_SYNC_TOKEN with a long random secret.
docker compose -f docker-compose.sync.yml up -d --build
```

The port published by default is `8087`. To change it, set `ALDUNE_SYNC_PORT=9000` in `.env` and
configure a URL such as `http://192.168.1.20:9000/` in Aldune. In Aldune select `Self-hosted server`,
enter the same token and use the same sync code on all devices. The token
must be just the value to the right of `ALDUNE_SYNC_TOKEN=`. The `Sync now` button is
available in Settings and in the dock; you can also turn on periodic sync and choose the
interval. Settings shows the last successful sync.

Without a token the server refuses to start, so the failure is immediate and visible, not silent.

In Settings you can also choose between `All notes`, `Selected notes` or `Notes with a
tag`. In the second mode, only the notes ticked in `Choose notes…` are published or downloaded
on that link. In the third, only the notes with the chosen tag are exchanged.
The selection is saved locally and is not sent to the server, except for the tag name inside
a profile invitation code.

The server is a blob store and does not need SQLite. The logical volume `aldune-sync-data` holds
the objects and survives container updates. Its physical name is set with
`ALDUNE_SYNC_VOLUME` (default `aldune-sync-data`).

## WebDAV, Nextcloud and ownCloud

In Settings you can choose `WebDAV / Nextcloud` to use your own WebDAV folder or the one from
Nextcloud/ownCloud. Enter the URL of a folder that already exists, the user and, preferably,
an app password. Aldune creates the `objects/` folder inside it and stores the per-note encrypted
envelopes there. The password is protected locally on Windows and never goes into the invitation
code or gets uploaded as part of the data.

For access from outside your network always use an `https://` URL. On a Nextcloud installation, the URL
usually looks like this:

```text
https://cloud.example.com/remote.php/dav/files/user/Aldune/
```

The profile invitation can carry the WebDAV URL, but each device must enter
its own account or app password. The WebDAV folder must allow `MKCOL`, `PROPFIND`,
`GET`, `PUT` and `DELETE`; most Nextcloud and ownCloud installations already do.

### Nginx + Cloudflare Tunnel

If the server already uses Nginx and Cloudflare Tunnel, this is the recommended variant. Do not also run
the Caddy composition: both would try to take the same proxy ports.

Use `docker-compose.sync.nginx.yml`. So that only Nginx/cloudflared can reach the server, the `ports`
block must look like this:

```yaml
ports:
  - "127.0.0.1:${ALDUNE_SYNC_PORT:-8087}:8080"
```

If you remove `127.0.0.1:` and leave `"${ALDUNE_SYNC_PORT:-8087}:8080"`, Docker listens on all
interfaces and allows direct access from the network. That must not be exposed to the Internet without HTTPS and a
proxy or firewall in front.

The versioned configuration uses the network-open variant so that a proxy in another
container can reach the service. If the traffic comes in through a proxy running directly on the host,
add `127.0.0.1:` to restrict it. If Nginx Proxy Manager or another proxy runs in another container,
`127.0.0.1` points to that container, not to the host: use a shared Docker network or the host's
address and keep the open binding only if the network and the firewall allow it:

```bash
cp .env.example .env
# Edit .env and define a long secret:
# ALDUNE_SYNC_TOKEN=a-long-random-secret
docker compose -f docker-compose.sync.nginx.yml up -d --build
curl "http://127.0.0.1:${ALDUNE_SYNC_PORT:-8087}/health"
```

In Nginx, create a host for the chosen subdomain, for example `sync.example.com`:

```nginx
server {
    listen 80;
    server_name sync.example.com;

    location / {
        proxy_pass http://127.0.0.1:<LOCAL_PORT>;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

In Cloudflare Tunnel add a `Published application` route with this local destination:

```text
Hostname: sync.example.com
Service:  http://127.0.0.1:80
```

Cloudflare terminates the public HTTPS and the tunnel connects to Nginx over localhost; there is no need to open
the server's local port on the router or publish it directly to the Internet. In Aldune configure
`https://sync.example.com/` and use the same token and sync code as the other devices.

### Updating the server from Git

The server does not need a manual copy of `src/` or of the published Windows files. The
`Dockerfile` builds `src/Aldune.SyncServer` and its reference to `src/Aldune.Core` inside Docker,
so it is enough to keep a checkout of the repository and rebuild the image. The synced objects
stay in the Docker volume and the `.env` must live outside the checkout so that
secrets never enter Git.

**Since the version that runs the container as a non-root user** (UID 1654, `app`): an existing volume was created by root and the
server could not write to it. Before rebuilding you have to hand it over once (the volume name is `aldune-sync-data` unless
you use `ALDUNE_SYNC_VOLUME`):

```bash
docker run --rm -v aldune-sync-data:/data alpine chown -R 1654:1654 /data
```

A new volume needs nothing: it inherits the owner of `/data` from the image.

Recommended initial setup on a Linux server:

```bash
sudo mkdir -p /opt/aldune-sync
sudo git clone --depth 1 https://github.com/NachoPola16/aldune.git /opt/aldune-sync
sudo install -m 600 /dev/null /etc/aldune-sync.env
sudo nano /etc/aldune-sync.env
```

In `/etc/aldune-sync.env` define at least a token, and keep any existing server
configuration too, for example:

```dotenv
ALDUNE_SYNC_TOKEN=a-long-random-secret
ALDUNE_SYNC_PORT=8087
# Docker volume name (default aldune-sync-data):
# ALDUNE_SYNC_VOLUME=aldune-sync-data
```

For the recommended variant with Nginx and Cloudflare Tunnel, run the first update like this:

```bash
sudo env ALDUNE_ENV_FILE=/etc/aldune-sync.env \
  bash /opt/aldune-sync/scripts/update-sync-server.sh
```

On later versions you only need to repeat that command. The script does a `git pull --ff-only`
of `main`, validates the composition, rebuilds the image with `docker compose build --pull` to
refresh the .NET base images too, keeps the data volume and checks `/health`, retrying for up to 30 s because the container takes
a few seconds to accept connections (it exits with 0 if it answers and with 1 and a message if not). It does not
copy `src/`, does not modify the `.env` and fails before updating if the secrets file is missing.

The volume holds the synced envelopes and survives the container being recreated, but it still
has to be included in the server's backups. Do not run `docker compose down
-v` or `docker volume prune` as part of an update: those operations can delete data.

This manual pull was chosen over deploying straight from GitHub Actions because the server
needs neither an SSH key nor a production secret in GitHub, does not require exposing Docker to the outside, and
the change can be reviewed before applying it. If the repository were made private, use a read-only deploy key
or a GitHub App with minimal permissions; do not store a personal token in the repository.

If you use another composition, you can select it without editing the script:

```bash
sudo env ALDUNE_ENV_FILE=/etc/aldune-sync.env \
  ALDUNE_COMPOSE_FILE=docker-compose.sync.https.yml \
  ALDUNE_HEALTH_URL=http://127.0.0.1:8087/health \
  bash /opt/aldune-sync/scripts/update-sync-server.sh
```

To deploy from another directory you can use `ALDUNE_REPO_DIR`; for a different branch,
`ALDUNE_BRANCH`. The public repository needs no credentials for the `git pull`; if it is made
private, configure a read-only deploy key on the server, never a token inside this
repository or the sync file.

If you manage the tunnel through `config.yml`, the equivalent rule is:

```yaml
ingress:
  - hostname: sync.example.com
    service: http://127.0.0.1:80
  - service: http_status:404
```

Validate the configuration with `cloudflared tunnel ingress validate` and create the DNS with
`cloudflared tunnel route dns <TUNNEL> sync.example.com`, if you have not created it from the dashboard.

### HTTPS with Caddy

To expose the server with automatic HTTPS through a public domain, use the composition
prepared in `docker-compose.sync.https.yml`. It requires the DNS of `ALDUNE_SYNC_DOMAIN` to point to the
server and ports 80 and 443 to reach Docker:

```powershell
Copy-Item .env.example .env
# Also add to .env:
# ALDUNE_SYNC_DOMAIN=sync.example.com
docker compose -f docker-compose.sync.https.yml up -d --build
```

Caddy obtains and renews the certificate, and only Caddy is published to the Internet; `aldune-sync` stays on
Docker's internal network. In Aldune configure `https://sync.example.com/`. Caddy's data lives
in the `caddy-data` and `caddy-config` volumes, separate from Aldune's encrypted envelopes.

If the server is only reached over a VPN or the local network, keep `docker-compose.sync.yml` and use HTTP inside
that network. Do not disable TLS verification in the client to get around certificate errors.

Token rotation and the conflicts interface are already implemented. For real HTTPS, the composition
still has to be tested with a domain and DNS controlled by the user.

### Failed-attempt limit

The server counts wrong tokens per address, in a one-minute window and only in memory. From **10 failures per minute**
that address gets `429 Too Many Requests` with `Retry-After`, **even if it gets the token right**: otherwise guessing would still work.
Other addresses are not affected and `/health` is never limited. A correct token does not clear the failures: only time releases them.

| Variable | What it does |
|---|---|
| `ALDUNE_SYNC_MAX_AUTH_FAILURES_PER_MINUTE` | Maximum failures per minute and address. Default 10; `0` turns it off. |
| `ALDUNE_SYNC_TRUST_PROXY` | `1` or `true`: the address is the last entry of `X-Forwarded-For` (the one your proxy added). Without it, the connection's address. |

Without `ALDUNE_SYNC_TRUST_PROXY` the header is ignored, so nobody can escape the limit by forging it. But behind a proxy **all**
connections arrive from the proxy's address: a single client with the token wrong would block everybody. That is why `docker-compose.sync.https.yml`
(Caddy, which sets `X-Forwarded-For`) turns it on by default; in the Nginx variant (`docker-compose.sync.nginx.yml`) the limit comes **off** (`0`) and, to turn it on, set `ALDUNE_SYNC_TRUST_PROXY=1` and `ALDUNE_SYNC_MAX_AUTH_FAILURES_PER_MINUTE=10` only if that proxy writes the header
(`proxy_set_header X-Forwarded-For $remote_addr;` in Nginx, so it does not forward what the client sends) and there is no other way to reach the server's port.
If after changing a token you see 429, some device is still on the old one: fix it and wait a minute.

### Token rotation

The server still accepts the single token `ALDUNE_SYNC_TOKEN`. To rotate it without cutting off
devices, temporarily use the list `ALDUNE_SYNC_TOKENS` in `.env`:

```dotenv
ALDUNE_SYNC_TOKENS=new-token,old-token
```

Recreate the container, change the token on all devices and check that they sync. When
all of them use the new one, leave only `ALDUNE_SYNC_TOKENS=new-token` and recreate the container again: the
old one is revoked. The list takes priority over `ALDUNE_SYNC_TOKEN`; never put tokens in
the URL or in the repository.

### Sync profiles

You can create several profiles from Settings. Each profile keeps its transport, server or folder,
token, sync key, selected notes and last sync separately. The old
configuration is migrated to the `Mis dispositivos` profile the first time it is opened. To
share a note with another person, create another profile, select only that note and share that
profile's code; the future invitation and revocation flow for profiles is still pending.

**All enabled profiles sync at once** (2026-10-27), even if it is not the one chosen in the dropdown: that
dropdown only decides which one is being edited. “Sync now” and the automatic polling go through the profiles with
“Sync this profile” ticked, the chosen one first; the failure of one does not stop the others (the error names the profile) and the
automatic interval is the shortest of the automatic profiles. Each profile keeps its **conflict bases** separately
(`SyncBase` and `SyncConflict` carry `ProfileKey`): without that, what was agreed with one store would hide a real conflict in another. The
first profile keeps the empty key, so the bases that already existed are still its own; the others use their id
(`SyncProfileSettings.BaseKey`). An earlier version opening the database keeps working. When a profile is deleted its
bases are left orphaned in the database (harmless: the id is not reused).

### Sharing a selection of notes

In the source profile, create or select a profile, tick `Selected notes` and choose the notes.
`Share profile` copies an invitation that contains the key, the identifiers of that selection,
the profile name and, if it uses your own server, its URL. On the other device, create the profile,
paste the code and click `Import code`; the transport and the server URL fill in by themselves,
but the token must always be entered separately. The token never travels inside the code. Codes
are generated with the prefix `aldune-profile-v2`.

## Future compatibility

Compatibility is controlled by the sync format, not by Aldune's visible version number.
Updates that keep the same format can sync with each other. If a version
introduces an incompatible change, the format goes up and each client accepts only the window it knows how to
read (`MinimumSupportedFormat`-`CurrentFormat`). A version that is too old or too new returns
a clear error and does not apply partial data.

The envelope format and the API are JSON/HTTP and do not depend on Windows, WPF, SQLite or DPAPI. A
future client for Android, iOS, macOS or Linux can reuse the server. DPAPI only protects the
key and the token stored locally in the Windows version.

Format version 2 also syncs tags inside the encrypted content. Format 3
adds protected notes. Format version 4 adds each note's position within the deck
(`NoteOrder.Position`), so that all devices show the same tab order; window
positions stay local to each device, because the same desktop makes
no sense on different monitors. An old client rejects the envelopes it cannot read with a
clear error instead of applying partial data: an old version would rewrite the note without the
position and silently undo the reordering.

Pinned notes (Windows 2.2.0 / Android 1.1.0) do **not** raise the format: the pin travels inside the encrypted note as the optional field `pinnedToTop`, written only when it is true. A

Per-note “done tasks” setting (Windows 2.3.0 / Android 1.2.0) does not raise the format either: it travels as the optional field `completedTasks` (an integer: 1 = keep in place, 2 = move to the bottom), written only when it is not 0 (“follow this device's settings”). Readers ignore unknown fields, and a number they do not know reads as 0. Notes with 1 or 2 are never deleted (Windows) or hidden (Android) by the device's own “done tasks” settings, which is what protects a shared list. As with the pin, an older version that *edits* and re-sends the note drops the field (the note goes back to “follow this device's settings”). The version history is **local**: it is never part of the payload and does not sync.
version that does not know it ignores it and shows the note unpinned; if that older version **edits the note and sends it back**, the pin is lost (an unedited note is not re-sent, so nothing
changes unless you edit it there). Pinning or unpinning counts as an edit of the note, so it syncs like any other change.

The selection can be different in each profile. To share a note with another person, create a
separate profile, select only that note and share that profile's code.

### Revoking an invitation

In Settings, click `Revoke old codes` inside the profile you want to protect. Aldune
first syncs the current version, generates a new key and re-encrypts the link's envelopes.
Devices that keep an old code will stop syncing; then generate a new
invitation and share it only with those who should continue. The server token does not change. If the
application is closed during the process, the next sync tries to finish the rotation.

Revocation prevents decrypting future versions of the store, but cannot delete a copy that another
device had already downloaded before revoking.
