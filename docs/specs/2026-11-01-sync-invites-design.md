# Invitations to a shared sync server — design (2026-11-01)

Status: **decided, not built.** Goal: a person you trust gets their own space on your server from inside the app, without anyone logging into the server.

## Why this and not the alternatives

- *Open registration with approval*: the server would answer strangers; more attack surface and a queue to look after.
- *Only a help link + an `add-space` script*: the owner still enters the server for every person.
- *Invitation codes* (chosen): nobody gets in without a code the owner made; the server never talks to an unauthorised person beyond a rate-limited 401.

## Flow

1. The server has a new optional `ALDUNE_SYNC_ADMIN_TOKEN` (long, random, never the same as a store token). Without it nothing below exists (404): existing servers do not change.
2. **Owner, Windows** (Settings → Sync → *Invite someone*; enters the admin token once, kept protected like the sync token): the app calls `POST /admin/invites` and shows a one-use code (128 random bits, valid 7 days by default, optional label). Share it as text or QR.
3. **Guest, Windows or Android** (Settings → Sync → *I have an invitation*): pastes the code and the server URL (or both inside one invite string: `server + code`, no key, no token). The app calls `POST /invites/redeem`; the server creates the space, generates a token (256 bits), returns it **once**, and burns the code.
4. The app creates a sync profile with that URL and token and a **new sync key generated on the guest's device**. The server never sees the key.
5. Owner can list and revoke spaces/invites from the same screen (`GET /admin/spaces`, `DELETE /admin/invites/{id}`, `DELETE /admin/spaces/{name}` revokes the token and keeps the files, like today).

## Server details

- Dynamic spaces need a registry the env var cannot give: `/data/spaces.json` (name → SHA-256 of the token, created, label). Env spaces stay and win on a name clash; the server refuses to start on a token clash, as now. Invites are stored hashed too.
- Redeem and admin routes share the existing per-address failure counter, so guessing a code or the admin token is throttled like guessing a store token. Codes are compared in constant time.
- A redeemed space name is generated (`u-` + 8 hex); the label is only shown to the owner.
- Optional `ALDUNE_SYNC_MAX_SPACES` (default 20) and per-space size cap so an invitation cannot fill the disk.
- The admin token can create and revoke spaces but **cannot read any store** (files are end-to-end encrypted and the admin has no keys anyway).
- Sync format stays **4**; apps that do not know invitations keep working (they just use a token).

## Not in this design

- No `aldune://` join links (see ROADMAP, discarded: a tappable link has the hostile-link risk). The code is pasted by hand.
- No open sign-up page, no email.
- Android is guest-only at first (like profile creation, owner tools stay on Windows); owner tools on Android can follow.

## Tests

Core/server: code single-use, expiry, wrong code throttled, space isolation with a registry space, admin token cannot read a store, env + registry clash refuses to start, revoked token → 401. Probe: a real redeem from the Windows app against a temporary server.

## Help inside the apps

Settings → Sync gets a *How do I set up a server?* button on both apps that opens the Docker guide in `docs/SYNC.md` (GitHub). No in-app instructions: they would go stale.
