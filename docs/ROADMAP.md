# Aldune — Roadmap and pending decisions

Sibling document of `STATUS.md`. That one tells **what is done and why**; this one keeps **what has
not been done, what was discarded and for what reason**, so that it does not have to be investigated
again or argued from scratch.

Originating session: 2026-09-06.

---

## Visible version of the application

The current round is identified as **v2.5.0**. The version is shown in Settings and also in the tray
icon's text. Every major update must increment this number following SemVer: patches for fixes, a
minor version for new features and a major version when there are incompatible changes.

## 1. Inspiration

Aldune was born to bring to Windows a concept that exists on macOS: notes anchored to the screen edge that fan out. It was
inspired by [Hold My Notes](https://holdmynotes.app/) and [noty](https://github.com/aimen08/noty) (open source). On
Windows there was nothing with that model of a deck anchored to the edge, and Aldune's design came out of that. It remains a principle that
the app works **without an account and with encrypted content**, and that any cloud is optional.

---

## 2. Pending and decided: it will be done

### ~~Revisit the note title~~ — RESOLVED (option E)

The four alternatives were laid out and rendered side by side (script in the session's scratchpad) and
in doing so the missing argument appeared: **the note text is stored encrypted in a single
block**. A title stored separately would have to be encrypted on its own (its own blob, nonce and tag) or
go in the clear, leaking precisely the most descriptive part of each note. And putting it inside the same encrypted
block is, literally, "the first line".

From that came a fifth option that was not on the table and is the one that was implemented: **the header
shows and edits the first line, and the body starts on the second** (`Aldune.Core.NoteText` splits and
recomposes). No duplicate, no empty header, with an editable title, and without changing absolutely anything
about how it is stored or encrypted.

What remains noted in case it is picked up again: joining with Backspace at the start of the
body (merging the line with the title) was **not** implemented. Doing it properly requires line-merging logic; doing it
halfway feels broken. You can go up with the Up arrow, which is what any control does.

### Historical note: the earlier discarding of the title (in case the debate returns)

The user explicitly said that **the current solution does not convince them** (the header text was removed;
the title is the first line of the body). What remains pending is to give it another look **with
alternatives laid out and rendered**, not just argued — the technique of rasterizing in
isolation with `RenderTargetBitmap` already helped decide the checkbox glyphs and applies
equally here.

Alternatives to put on the table, with their render side by side:
1. As it is now (header without text).
2. Header with the title, as before (accepting the duplication).
3. A truly editable title, separate from the body — now cheap, because **there is no data to
   preserve**; the migration argument given at the start was not valid and the user pointed
   this out rightly. See the discarding below for the reasons that do hold and the one that would
   justify it.
4. The header shows the first line **only when the body is scrolled** and that line is no longer
   visible. It resolves the duplication and the only case that is lost today, in exchange for a
   non-obvious behavior rule.

### ~~Reorder notes by dragging in the deck~~ — DONE

See `STATUS.md`. The design decision is kept here in case it has to be picked up again: a separate `NoteOrder`
table with `Position REAL`, to move a note by writing a single row instead of renumbering.

### ~~Visual overlap when dragging downward~~ — IMPLEMENTED (pending visual verification)

When dragging a tab downward, it must visually pass in front of the tabs it
crosses, just as when dragging upward. The logical order and persistence already work;
during the gesture the selected button is raised and, above all, its real `ContentPresenter`/container
in the `ItemsControl`, which is what decides the layer between overlapping tabs. On release those values are restored
and the normal order is recalculated. What remains is to check visually in the dock that a tab
dragged downward ends up in front of all the ones it crosses.

### Reorder notes by dragging in the deck (original design, already implemented)

Today the order is `CreatedAt` and cannot be changed. In a deck-of-cards metaphor, not being able to
decide which one is on top is a real gap; noty has it.

**Design decision already made**: the position is **not** stored by adding a column to the `Note` table
— that table holds real user data and this app has no migration system. It is done
with a separate table, `NoteOrder (NoteId TEXT PRIMARY KEY, Position REAL NOT NULL)`, just as was
done with `NotePlacement`. `Position` is `REAL` and not `INTEGER` **on purpose**: it allows inserting between
two notes (arithmetic mean of their positions) without renumbering the whole list on every drag.

### ~~Dock on the left does not look right~~ — RESOLVED (see `STATUS.md`)

Real cause, found with the user's screenshots (2026-09-09): `RestStrip`, the fan's tabs
and the button footer were aligned "to the right" **in the XAML itself**, without conditioning on the
edge — a value correct only for `EdgePosition.Right`, which was the only one this was originally
designed with. The dock window (`EdgeGeometry.WindowRect`) was already positioned correctly against the
physical screen edge with `DockEdge = Left`, but that inner content was still aligned against
the wrong side (the inside of the dock, not the outside), leaving the gap seen in the
screenshots. Fixed by mirroring those three alignments in code when the edge is Left — see
`STATUS.md` for the technical detail. `EdgeDockWindow.PositionNoteWindow` (where the note itself
opens) never had the bug: it already distinguished the edges correctly.

**Second round, same underlying cause**: the shape of each tab itself (`CornerRadius`/
`BorderThickness`/shine gradient in `NoteTabButtonStyle`) was also designed only for the
right edge — rounded on the left, flush on the right. Mirrored in the same way, verified with an
isolated render before touching the real XAML.

**Third round, the true root cause**: there was still a gap (strip, fan and footer buttons
alike) because the `Grid` that wraps *all* of the dock's content has its own asymmetric
`Margin="18,18,0,18"` in the XAML — no margin on the right, correct only for
`EdgePosition.Right`. No adjustment inside that `Grid` could compensate for the container itself
already being shrunk on the wrong side. Mirrored as well. Full detail in `STATUS.md`.

### ~~Top and Bottom edges for the dock~~ — DONE, only an optional future design review remains

The user explicitly asked to design the **Top** and **Bottom** edges too — the original spec for the
fanned tabs excluded them on purpose ("it would require sliding vertically, which is out of
scope for this round", see the historical note in `EdgeDockWindow.PopulateEdges`, which at that time only offered
Left/Right in
Settings). **Finding while investigating the bug above**: `Aldune.Core.EdgeGeometry` (the pure geometry,
with tests) **already covered all four edges**, but the first Top/Bottom layout was incorrect:
it treated a side card as if it could be rotated without changing its measurements. The corrected design
keeps a horizontal bar at rest and, when unfolded, opens a vertical column of
208px-wide cards, just like the side ones, so that the title keeps its room. Top grows
downward and Bottom upward; if there are many notes, the scroll is vertical. The dock thickness,
the length budget, dragging and opening of notes now use that specific geometry.
The current implementation is closed for this round. The alternative of wide cards stacked
inward is kept only as a possible future visual review.

### ~~Note layout templates on the desktop~~ — DONE

User idea (2026-09-09): instead of dragging each note by hand to leave them tidy,
offer automatic layout templates (grid, uniform cascade, columns...) that
place them all at once. It connects directly with "Open all notes" (see `STATUS.md`, same
session): today that function only cascades with a fixed diagonal offset
(`EdgeDockWindow.NoteWindowCascadeStep`/`NoteWindowMaxCascadeSteps`), which with several notes open at
the same time leaves a messy pile instead of something "tidy".

**The current free placement (dragging each note wherever you want) must remain available** —
templates are one more option, not a replacement. It fits with `AppSettings.RememberNotePositions`
as it exists today: it will remain the default mode, and a template is something the user asks for
explicitly when they want to tidy up all at once, not something imposed.

Unanswered questions for when its design session comes up:

- Which templates to offer at the start? Grid and uniform cascade seem the two obvious ones; any more?
- Does it apply only when using "Open all", or also as a separate action on notes that are already
  open (rearranging them without closing them)?
- ~~Interaction with `AppSettings.RememberNotePositions`: applying a template overwrites each note's
  saved position. Is that memory simply lost, is the user asked first, or can it be undone?~~
  **Resolved** (2026-09-16, see `STATUS.md`): the template's movement does not save the position — it is
  flagged as a coordinator relocation (`NoteWindow._isLayoutMove`) — so each
  note's memory survives while it is laid out; it is only lost if the note is closed while laid out, which is the
  general rule of "remember the last position". And it can be undone: "Restore original
  positions", in the same menu.
- ~~On multi-monitor, is the template applied per screen (each dock lays out only its own) or does the whole
  set of monitors have to be considered at once?~~ **Resolved** (2026-09-16): all onto one
  screen, that of the dock that is clicked — a layout spread across two monitors is not a
  layout —. If there are more screens than docks, the menu lets you choose which one (see `STATUS.md`).

### ~~Contextual dock actions~~ — DONE, with optional future improvements

First delivery implemented: right-clicking the open-all button offers normal mode,
grid, columns and close all. The choice is saved as the button's default
mode; normal mode remains the initial value and must be chosen
explicitly to return to it. The template is applied on the screen of the dock that is clicked (or on the one
chosen, if some screen is left without a dock) and only changes positions when the user chooses it. Layout
changes are briefly animated to avoid abrupt jumps.
The grid and the columns are sensitive to the number of notes and to the screen orientation:
up to 12 notes are arranged in a compact template; the remaining ones stay open in a
readable cascade to avoid compressing them until they become unusable.

Right-clicking a note already has real use: open it, change its color, archive it or
send it to the trash. What remains to decide is whether the dock footer buttons should also offer a contextual
menu, without taking away their normal left-click action. Initial ideas:

- `+`: create a note from the clipboard or create it with a quick template.
- "Open all": choose a layout (cascade, grid or columns) before opening them.
- "Manage notes": open Active, Archived or Trash directly.

Filling the dock with advanced options must be avoided: the contextual menu should only provide shortcuts that
are hard to discover or that save several steps.

### Tags, dock views and selective sync — first delivery

A first usable base has been added: notes have persistent tags, editable from the
tab's contextual menu, and the dock can show active, archived, trash or only the
active ones of a tag. The trash does not appear in the normal view. Right-clicking the management
button opens that selector directly, to keep the main buttons without filling them with
submenus.

Tags are part of the encrypted sync format 2. Format 1 can be read,
but an old version rejects format 2 so as not to silently delete tags when saving
a later version. What is left for the next round is designing selective sync of notes,
multiple sync profiles and sharing a note with another person; those
identities must not be mixed with the current device code.

The first part of that next round is already implemented: in Settings you can choose to sync
all notes or a specific selection. The selective scope is local to the installation and excludes
both publishing and downloads outside the selection. Independent profiles and the code
to share a selection are already implemented; what remains pending is improving the invitation
and revocation flow without mixing it with the “My devices” link.

### Simplified mode / full mode — IMPLEMENTED

Offer in Settings a clear button to switch between a **full version** and a **simplified
version** of Aldune, intended for those who want to use notes without so many options. It would not be another
installation or a different database: it would be a reversible, persistent interface mode.

The simplified mode should keep the core (dock, create/open notes and basic manager) and hide
the advanced configuration, such as monitor selection, edge, global shortcut, full screen,
trash retention and task automation. The button must allow returning to full mode without
losing or resetting settings; switching modes cannot touch the notes.

Pending to decide in a design session:

- whether the change applies instantly or asks for a restart;
- whether the simplified notes manager keeps archive/trash or only the active notes;
- which text and location of the button make it obvious that you can go back to full mode.

### ~~Free note color, in addition to the palette~~ — DONE

User idea (2026-09-11): in addition to the 6 colors of `NoteColorPalette`, being able to choose a free
color for a specific note. **The current palette stays as the default** — same pattern as
"layout templates" and "sync" above: the structured out-of-the-box option remains
the normal path, and freedom is something asked for explicitly, not a replacement. Reason
for not touching the default: the palette is deliberately designed (six colors derived in
OKLCH with exactly the same lightness, so that "color is identity, not hierarchy" and no note
weighs visually more than another) — a freely chosen color can break that balance on purpose,
which is precisely what is sought by choosing it.

**Half of the infrastructure already exists**: `NoteColorPalette.RimFor`/`LabelFor` already compute a
reasonable border and label color for any hexadecimal outside the palette (darkening
proportionally) — today that path is only used for legacy notes with old colors, but it
would serve just as well for a hand-picked color. What is missing is the color picker itself (a Windows
`ColorDialog`, or something custom to match the rest of the app) and deciding where it lives: one more color in the row
of pills of the "⋯" menu that opens a picker, or a separate entry? Its own design session
when its turn comes — it is not big, but a color picker at note
creation was explicitly discarded earlier ("unnecessary complexity for the benefit", see below), so it is best not to
repeat that argument without thinking it over first.

### ~~Internationalize into English~~ — DONE (see `STATUS.md`)

It was not just translating: the user uses Aldune in Spanish daily, so a
Spanish/English selector was added in Settings (`AppSettings.Language`) instead of simply replacing. Texts in
`Aldune.Resources.Strings` (hand-written dictionary, not `.resx` — reason in `STATUS.md`). Changing the
language asks to restart the app to be seen in all windows.

**Gap left on purpose**: `HotkeyBinding.DisplayName` (Core) does not translate "Espacio"/"Supr"/"sin
asignar" — see the why in `STATUS.md`. If picked up again, that property has to be turned into a
parameterized method and `HotkeyBindingTests` touched, which fixes those three texts literally.

### ~~Export to Markdown / plain text~~ — DONE (see `STATUS.md`)

Both macOS rivals have it. Besides being useful it is a **trust feature** ("I don't
hold your data hostage"), which weighs more if one day it is charged for. On export, the `☐`/`☒` checkboxes are
translated to Markdown's task syntax (`- [ ]` / `- [x]`), as this item said — only
Markdown was implemented, not separate plain text (see the brainstorming in `STATUS.md` for the why).
One note at a time from `NoteWindow`, or in bulk from `NotesManagerWindow`.

### ~~Reminders with Windows notification~~ — DONE (see `STATUS.md`)

**An important functional gap compared with other Windows note apps** and one of the most requested features. A single
one-off reminder per note (In 1 hour / Tonight / Tomorrow 9:00, or calendar + manual time),
native Windows notice via the already existing tray icon, and a badge on the dock tab.
Full detail, including the 5 integration gaps found by the final review of the whole
branch (and fixed in a single round) and what remains pending manual verification by the user
(the visual notice on screen), in `STATUS.md`.

**Not implemented, still pending if picked up again**: snoozing a reminder that has already gone off (today it has to be
set from scratch, with the same panel as always — there is no "snooze 10 minutes" shortcut on a
notice that has just gone off).

### Sync between devices — base implemented, with two paths

It used to be parked as "discarded for now, do not touch until someone really asks for it" —
the user asked for it and it now moves to incremental implementation. The initial core already uses JSON envelopes
with per-note encrypted content, a shared key independent of DPAPI, tombstones, atomic writes
and deterministic resolution by date/device. The next block shows the configuration,
allows manual sync and offers periodic background sync.
What was already decided in the initial design, so as not to argue it again from scratch:

- **Both paths, not just one** — explicit user decision after seeing the trade-off:
  1. **Folder chosen by the user** (OneDrive, Google Drive, Dropbox, Syncthing — any that
     already syncs a normal disk folder). Aldune never talks to any cloud: it stores one
     encrypted file per note inside that folder and rebuilds. It keeps "no account, no
     server", works with any provider (including none, with Syncthing), and is the path that
     naturally also serves a future client on another operating system (Android/iOS,
     mentioned by the user as possible but undecided) — that future client would sync by
     reading the same folder through its own Drive/OneDrive app, without Aldune having to
     talk to anyone's API.
  2. **Aldune talks directly to the Google Drive API** (OAuth, with no desktop client in
     between). Explicitly requested despite the cost, so let it be noted for when it is designed:
     it needs the Google login flow, storing and renewing a token (delicate, same family of
     problem as the encryption key — see `DatabaseKeyProvider` in `STATUS.md`), registering the app
     in Google Cloud and, if Aldune is ever published, going through its OAuth app verification
     process (otherwise an "unverified app" warning appears). And it contradicts, for whoever uses it, the project's own
     principle ("no account, ever", §1) — which is
     why it has to be an **optional** path, never the only one, with path 1 always available by
     default.
- **Technical warning that still holds for path 1** (it was already noted before this was decided):
  do *not* sync the SQLite file directly — sync folders corrupt databases
  opened by two machines at the same time. One encrypted file per note, rebuildable, is the only safe
  way.
- **Self-hostable path, also decided** — it will not depend on an Aldune service:
  1. **Local folder, UNC or NAS**: for a folder shared on the network (`\\servidor\\aldune`) or
     a folder synced by Syncthing/Nextcloud/another client. It is the first implementation and
     covers the "my own server inside the house" case without adding a new API.
  2. **WebDAV over HTTPS**: for a server reachable from outside the network, especially
     Nextcloud/ownCloud or a WebDAV server of one's own. Aldune will store the URL and an application
     credential, not the main password when the server offers that possibility.
  3. **Dedicated Aldune API**: remains as a future option, only if WebDAV does not cover some real case;
     it would imply maintaining its own server, authentication, TLS, versions and migrations.
- **Proposed implementation order**:
  1. transport-independent sync core, with per-note encrypted files,
     manifest, deletions represented by *tombstones*, atomic writes and recovery;
  2. local folder/UNC/NAS transport;
  3. self-hostable WebDAV transport;
  4. direct Google Drive as a later optional integration (OAuth and token renewal).
- **Key shared between devices** — the current DPAPI protection remains only to protect the local
  copy. For another machine to be able to decrypt the notes, an independent sync key will have to be introduced,
  transferable through a recovery code/phrase and protected locally on each
  machine. A DPAPI key tied to a single Windows user will never be uploaded.
- **Recommended initial policy** — `Guid` to avoid collisions when creating notes, `UpdatedAt` plus a
  deterministic per-device tiebreaker for conflicts on the same note, and *tombstones* so that
  a deletion does not reappear when syncing an old machine. The interface will have to show status,
  last sync, errors and recoverable conflicts.
- **Not yet decided, for the design session**: what happens if the same note is edited on two
  devices before syncing (probably "the most recent wins" by `UpdatedAt`, given that
  this is a personal tool for one person, not collaborative — but it has not been confirmed with the
  user); exact format of the per-note file; how to detect remote changes without a client that
  notifies (watch the folder with `FileSystemWatcher`, poll at startup, both?); what happens if two
  devices create a new note "at the same time" (with `Guid` as Id, it should not collide,
  but it is worth confirming explicitly in the spec).

#### Self-hosted server with Docker Compose

The first version of the self-hostable server is added as `src/Aldune.SyncServer` and can be brought up
with `docker-compose.sync.yml`. It is an HTTP store of encrypted objects, not a database with note
text: it stores the envelopes without knowing the key. The user configures an IP/hostname and port (by
default `8087`) in Aldune, along with an access token. The Docker volume keeps the objects even if
the container is recreated.

The API and the format do not depend on Windows: a future Android, iOS, macOS or Linux client will be able to
use the same HTTP endpoint and the same shared key. DPAPI only protects the local copy of the token and
of the key on Windows; it is never sent to the server.

HTTPS behind a reverse proxy, token rotation and the conflict-resolution interface are already
prepared or implemented. What remains is testing the real HTTPS deployment with a domain of the user's.

#### Next sync review

Before considering this first version closed, this checklist is noted:

1. ~~**Validate real conflicts**~~: done with two independent databases and a shared folder.
2. ~~**Secure access from outside the local network**~~: HTTPS composition with Caddy prepared; validation
   on the real server is missing.
3. ~~**Harden authentication**~~: token rotation documented through `ALDUNE_SYNC_TOKENS`.
4. ~~**Conflict interface**~~: review, restore or discard the losing version.
5. **Cross-platform clients**: reuse the format and the API for future Android,
   iOS, macOS and Linux versions.
6. ~~**Compatibility between versions**~~: window of supported formats and clear rejection of
   incompatible versions already implemented.

#### WebDAV block implemented

Guided import and safe revocation of profiles are already implemented. Revocation stores
a pending key and re-encrypts the envelopes before activating it; if the application is closed
midway, the next sync resumes the process. Devices with the old code must
import a new invitation. The next block is the WebDAV transport over HTTPS for servers
such as Nextcloud and other self-hostable services.

The interface now allows configuring WebDAV/Nextcloud with a username and a locally protected password.
The invitation carries the URL, but not credentials. For the Internet, HTTPS must always be used.

#### Next recommended block

Test WebDAV against a real Nextcloud/ownCloud installation and, afterwards, prioritize the
cross-platform clients reusing the common sync format.

---

### Round after 2.2.0 / 1.1.0 — decisions (2026-10-31/11-01)

- **Done tasks per note, not per device**: a device-wide "delete done tasks" would empty a shared list on every device. The per-note value (follow settings / keep in place / move to the bottom) travels as the optional field `completedTasks`; the last two never delete or hide. Format stays 4.
- **Version history is local and never syncs**: no format change, nothing extra to protect; protected notes keep none. Android keeps the local text before a sync overwrites it, like Windows.
- **Tag order on Android uses arrows, not drag**: larger touch targets and TalkBack. The order is per device (not synced); Windows got the same in 2.5.0: `TagOrder` in settings, arrows in the manager's «Manage tags», read by the dock menu, the wheel tour, the manager and the tag panel through `NotesRepository.GetAllTags`.
- **Wheel changes dock view**: gear always; tabs only when they all fit (a turn continuing a recent change keeps cycling); Ctrl+wheel always. The label sits next to the gear, towards the screen, so it never covers notes. No extra button on the dock (it would add weight where less was asked).
- **Not done / later**: capping the dock column height with very many notes on the top and bottom edges; filtering the Android widget by tag; checkable boxes in the widget. Italian and Japanese were added at the end of this round (machine-assisted, not reviewed by native speakers).

### Round 2.4.0 (Windows) — decisions (2026-11-01)

- **View label**: one pill, always in the same place — centred on the notes column at the free end of the strip (above the first note on the left, right and bottom docks; below the last one on the top dock, where the button bar hugs the screen edge). A custom popup placement never flips sides, which is what made it jump around the cogwheel. It fades on its own after about a second.
- **Filter indicator**: the notification-like dot on the cogwheel is gone (the glyph was never off-centre: ink measured at 0.0 px in most looks). An accent ring replaced it in 2.4.0 and was dropped in 2.5.0 (it read as a focus ring and shared the colour of «+»). Now the view label stays up above the notes while the view is not «Active» and the fan is open; folded, nothing floats. The tooltip still names the view.
- **Wheel session**: while the wheel keeps turning (1.5 s after the last notch) a global wheel hook, limited to the area the dock covered when the session began, keeps changing the view, so switching to a tag with few notes never leaves the cursor outside the dock. Nothing is painted: an almost transparent background was tried first and rendered as black bands on some systems.
- **Gap between the button bar and the notes**: 8 px visible on the four edges (was 16).
- **Pin colour**: the row's own ink (what the title uses) instead of the softer note «label» tone; filled glyph when pinned, outline at 0.45 when not.
- **Language**: still applied on restart (a live reload would rebuild every open window, notes with unsaved text included); Settings now says so under the list, with a «Restart now» button.
- **Settings header** uses the notes manager's spacing (20,18,14 and 20,17,16,17 with a real title bar, more air under it).
- **Tray menu**: the outline of the hovered row follows the look's accent, so a custom accent colour reaches it.
- **Links in notes**: allowed, in the smallest safe form. Plain `http://` and `https://` text opens with Ctrl+click (hand cursor while Ctrl is held), never by itself; nothing else opens (no `file:`, UNC, `javascript:`, `mailto:`), URLs with user info are ignored, runs over 2048 characters are ignored (`LinkText`, Core). The note stays plain text, so sync and storage do not change. On Android (1.2.0) the same rules apply (`LinkText` in the `text` module, twin tests): addresses are underlined and, with the cursor on one, a chip «Open <host>» appears under the editor; tapping the chip is the only way to open it, and the host is shown so the destination is visible. Plain taps still place the cursor.
- **Not changed, on purpose**: manager rows and tab previews keep their spacing (measured, nothing cramped). Closing a note used to fade only its content while the window frame and the DWM shadow stayed at full strength and vanished with a pop (worst with the XP looks). Windows refuses `WS_EX_LAYERED` on these WPF windows, so now `ClosingGhost` renders the note (background + content), cloaks the real window and fades a transparent stand-in with the same corners and a soft shadow; if the render fails, the old content fade still runs. 2.4.2 extends it to the notes manager and Settings through `WindowCloseAnimation` (they also faded only their content).

## 3. Discarded for now, with its reason

Do not propose again without reading this first.

### A shareable "join sync" link instead of the profile code — not for now (2026-11-01)

The profile code already travels as text, as a QR code, or with Android's Share to Aldune (one tap on the phone), and it never carries the token. A custom-scheme link (`aldune://join#code`) would save little: chat apps and mail clients do not make such links tappable, so the person would still copy and paste. An `https` link would need a page hosted for it (and Android App Links need a verified domain), which is a service to run and a new place where an invitation could be phished or logged. Putting the access token in a link is out of the question: a link is stored, previewed and indexed by chats. Revisit only if there is a hosted page for another reason.

### Hanging indent in lists and rendered markdown editor — discarded (2026-10-09)

Two ideas that ask for the same thing: replacing the plain `TextBox` with a rich-text editor.

- **Hanging indent** (so that the second visual line of a long, indented task sits under the text and
  not in the left margin). A WPF `TextBox` does not indent per paragraph. Tab already works (it indents the
  whole line, glyph and text) and so does having the next Enter inherit the indentation; there are no numbered lists.
  Approximating it by inserting real line breaks would pollute the note's text, and a custom editor
  risks Ctrl+Z, the clickable checkboxes and the height readjustment.
- **Notes linked to `.md`: edit raw, not rendered.** An editor with the markdown already compiled
  has to convert it back to text on save, and that round trip can rewrite the user's file
  (whitespace, `*` versus `-`, line breaks). Aldune never writes to a linked file in a
  way it does not control (see the linked notes spec). If one day you want to see the result:
  a separate read-only preview, without touching the editor.

It is reopened only if a way appears to do it without replacing the editor.

### Notes per Windows virtual desktop — discarded

An attractive idea (it would be something specific to Windows that no Mac app can copy), but the Windows
virtual desktop API (`IVirtualDesktopManager` and its internal COM interfaces) **is not
documented and changes between Windows builds**: the code would break by itself on every major
system update. It is not worth maintaining.

### Font picker — discarded; if anything, a size one

Decided with a comparative render of 8 Windows fonts. The body stays in the system
font and the title moves to Ink Free (see `STATUS.md`). A **family picker** is discarded for two
reasons, the second discovered while rendering:

1. According to the market research, in Windows note apps what people ask for is **font
   size** (readability, accessibility), not choosing a family.
2. Handwriting fonts **do not have the `☐`/`☒` glyphs**. Windows substitutes another font for them and
   they end up misaligned with the text — exactly the problem that was fixed by choosing `☒` for its
   metrics. Letting people freely choose a family reintroduces it, and moreover intermittently depending on the
   font each person picks.

If one day something is added, let it be **text size** (small / normal / large) applied to all
notes: it is what is asked for, it does not break the alignment and it is cheap.

### Quick capture without stealing focus — discarded (2026-10-04)

A minimal bar that appears with the shortcut, you type and it disappears. **Discarded because it is already covered**: the
global "new note" shortcut (`CreateAndOpenNote`) creates the note and leaves the cursor ready to type, and another
shortcut opens the last edited note. A separate bar would be one more window to maintain (focus, screens,
themes) to gain a gesture that already exists. If one ever really wants it "without a note window", it is
picked up again with a concrete use case.

### Persist "always on top" per note — DONE in 1.7.0 (`NoteUnpinned` table, only the notes that are released)

The switch already exists (the note's "⋯" menu) but it is per session: when the note is reopened it is pinned
again. Persisting it would require a new column in the `Note` table, which is the one holding the user's
real data and there are no migrations. It can be solved with a separate table if it is confirmed that someone
really uses it that way.

### Preview on hovering over a tab — discarded for now

noty has it, and the user proposed it as a way to make the title matter less. Two reasons
not to do it:

1. In a **fan** you have to pass over several tabs to reach the one you want,
   so a floating panel on hover would be appearing and disappearing all the time during a
   normal gesture. That is why noty offers it as an **option**, not by default.
2. Its main reason disappeared when the duplicate title was removed from the header: the tab already
   shows a title, a preview line and the task progress.

If picked up again, let it be as an optional setting and with a delay of ~500 ms before appearing.

### Drag to reorder lines within a note — discarded in favor of a shortcut

Explicit user request (2026-09-09): move checkbox tasks around by dragging them
inside the note body, as the deck already allows with whole notes. **The drag
itself** was discarded, not the possibility of reordering: the body is a plain `TextBox` on purpose (spec v1, no
rich text), so dragging lines in there requires simulating the gesture by hand — telling it apart from the
click that ticks the checkbox, drawing a ghost line that follows the mouse, rebuilding the text on
release — considerably more complex than anything done so far and in direct tension with that design
decision. Implemented instead (see `STATUS.md`): Alt+Up/Alt+Down swaps the cursor's line
with its neighbor, the same result without dragging anything. If dragging is really asked for again
with more insistence, the cost above is still the same — nothing has changed that makes it cheaper.

### Per-task reminder and "shopping list" note type — discarded (2026-09-26)

- **Per-task reminder**: already discarded by the reminders spec (a line has no stable
  identity) and the review of tasks confirmed it (identity by text failed in automatic
  deletion). Instead, the note reminder's notice shows the pending tasks.
- **"Shopping list" note type**: it would be a new field in the sync format, which earlier versions
  reject, and a second editor. It is covered with list functions valid for any note:
  Ctrl+Enter, uncheck all, delete the done ones, done ones at the end and the "All done" strip (see
  `STATUS.md`). If picked up again, the next sensible thing would be autocompleting with already-used items.

### Other minor discards

- **Tags and folders**: it is Microsoft Sticky Notes' complaint no. 1, but it clashes with Aldune's
  minimalism (the deck is active notes, period). If the list of notes grows, the right answer
  is probably to improve the manager and the search, not to add hierarchy.
- **Attaching images / screenshots** (other note apps have it): it would break the plain-text
  model, which is what makes simple encryption, search and export possible. Not without
  a very good reason.
- **Duplicate title in the note** — **resolved**: the header text was removed. The title is the
  first line of the body (right below) and what the dock tab shows; the header remains
  recognizable as "the tab that traveled with the note" by its color and its die-cut.

- **Editable title field, separate from the body — discarded by design, not by cost.**
  It is worth making clear why, because the first time it was argued badly: it was said that it would force a new
  column in the `Note` table and that there are no migrations. The user pointed out, rightly, that right now
  there is no data to preserve — so that argument does not hold. The reasons that do hold:
  1. **It does not solve the problem it seemed to solve.** The underlying complaint was a stack of tabs
     that all said "NEW NOTE". That happens because they are **empty** notes, and an empty note would
     also have an empty title: with a title field you would see exactly the same stack.
  2. **It costs one step on every note.** This app's gesture is "open and type". A title field
     puts a decision ("what do I call this?") in front of jotting down three words, which is 90 % of
     cases.
  3. Keeping a single piece of text is what makes encryption, search and export have no
     special cases.

  If it is ever picked up again, the argument in favor would be being able to name a note **independently** of
  its content (a long note whose first line does not describe it). That is indeed a valid reason, and it has
  nothing to do with the one given at the start.

---

## 4. If it is decided to publish

In order:

1. ~~Finish internationalization~~ — done: English, Spanish, German, French and Brazilian
   Portuguese (1.4.0). A native speaker still has to review the last three.
2. **Signed installer**. An unsigned `.exe` triggers SmartScreen and takes a good
   part of the downloads with it. Decided (2026-09-27): **free paths only**, in this order:
   - ~~**SignPath Foundation**~~ — **rejected on 2026-09-28**: they ask for public visibility signals
     (stars, forks, contributors, external articles or threads, sustained activity) that the
     project does not have yet. It can be requested again when it has them; the workflow is still prepared
     (it signs only if the variables exist) and what they required of the repository is in the git history
     (the "Code signing policy" section of the READMEs, removed when it was rejected). SignPath's paid
     subscription is out under the free-paths-only rule.
   - **Microsoft Store**, now the main path: developer registration has been free since 2025 and it signs the
     MSIX, but packaging as MSIX is separate work (startup with Windows, update notice,
     data paths) and it only signs what is installed from the Store.
3. Presentation page. What has to be said, in order of strength: it is the only one like it **on Windows**;
   notes are **encrypted and do not leave the machine**; **there is no account or cloud**; there is a
   **portable version** that does not need installing.

---

## 5. Appearance review — status

Done: note body freed from permanent buttons (they were ~90 px of 320, almost a
third), actions moved to the "⋯" panel, dark panel under the empty dock's buttons.

Pending, in order of importance:

1. ~~`SettingsWindow` can grow taller than the screen.~~ **Done** (see `STATUS.md`, session
   2026-09-07): first `MaxHeight` + `ScrollViewer` were added as a safety net, but the
   user reported with a real screenshot that the window kept running off the bottom — the underlying problem
   was not the lack of scroll, but too much height. It was redesigned into two columns (760px
   wide), verified with a mockup before implementing: the height drops by half. The
   `MaxHeight`/`ScrollViewer` are kept as a safety net for very small screens.
2. ~~**Two mixed control languages in Settings**~~ **Done** (2026-10-04, see `STATUS.md`): rounded cards (monitor, edge) coexist
   with loose checkboxes (startup, shortcut, full screen, positions). Unify: either all cards,
   or the checkboxes turned into right-aligned switches, Windows 11 Settings style.
3. ~~**Typographic scale**~~ **Done** (2026-10-04): there were 11/12/13/14/15/17 px scattered by eye across the four windows. Fix
   three sizes and apply them.
4. ~~**`NotesManagerWindow`** is the busiest window (452 lines of XAML) and the one that strays furthest from the
   minimalism of the rest.~~ **Reviewed** (2026-10-04, with a screenshot of the window with 4 notes): no longer true. The 681 lines
   of XAML are style templates; what you see is a header, a search, a sort and note rows, just as sober as
   the rest. No changes; the typographic scale was already applied to it.
5. ~~**Light theme for the chrome**~~ — **DONE** (2026-09-27): Dark, Light, Pastel, Midnight and
   Like Windows, live. See `STATUS.md`.
6. ~~The fan can stop being compact with many notes.~~ **Done** — see `STATUS.md`. The
   window has a cap (`EdgeGeometry.FanBudget`), the excess scrolls, and the rest strip only
   draws the dashes that fit.
7. **The text cursor collides with the "T" of "Title"** in the header when the title is
   empty — reported by the user with a screenshot (2026-09-12). The blinking caret of `TitleBox` is
   drawn at the same point as the placeholder, instead of leaving it its place.
8. **The "⋯" button does not close the menu when pressed again while open** — it reopens it
   instead (reported 2026-09-12). Suspicion: the `Popup` (`StaysOpen="False"`) closes itself on the "outside"
   click before `OnMenuClick` gets to run, so the toggle starts from `IsOpen` already
   `false` and reopens it — a classic WPF bug with popups that close themselves. Pending a fix.

7. ~~Title caret~~ **Done** (2026-09-12): the marker is hidden while the field has
   focus, leaving the cursor free.
8. ~~`⋯` menu that does not close on repeated click~~ **Done** (2026-09-12): the second click is
   captured before the `Popup` interprets it as an outside click.
9. **Custom note colors** — **Done** (2026-09-12): the quick palette is kept and
   the actions menu offers a free picker, limited to readable colors.
10. **Review of windows and motion** — **Done** (2026-09-12): the manager gains air and sharpness,
   blue focus rings are removed from the chrome and transitions become snappier.

### Sync conflict validation — DONE (2026-09-12)

The cases of editing on both devices and of deletion versus an old edit were reproduced with two independent
databases and a shared folder. The most recent version
wins and the remote device converges; a more recent deletion is kept as a tombstone and
does not make the note reappear. The tombstone envelope now uses the real identifier of the device
that publishes it so that the tiebreak is stable.

### HTTPS for the self-hostable server — PREPARED (2026-09-12)

`docker-compose.sync.https.yml` was added with Caddy as a reverse proxy, automatic certificates and
persistence of its configuration. The sync service is no longer published directly to the
Internet in that mode. What remains is testing it on the real server with a domain and DNS controlled by the
user; the local HTTP composition remains intact.

### Token rotation and revocation — DONE (2026-09-12)

The server accepts a temporary list of tokens through `ALDUNE_SYNC_TOKENS`, with priority over
the earlier single token. This allows publishing the new token, updating the devices and retiring
the old one without leaving sync unusable during the change. Revocation is done by removing
the token from the list and recreating the container.

### Interface for resolving conflicts — DONE (2026-09-12)

Conflicts no longer disappear after the automatic tiebreak: the losing version is kept locally,
encrypted with the database key. Settings shows how many there are and opens a window
with actions to restore that version or discard the record. Restoring the losing version gives
it a new timestamp so that the decision can sync again.

### Sync profiles — DONE (2026-09-13)

Settings allows creating several independent profiles. Each keeps its name, transport,
server or folder, token, sync key, note selection and last sync.
The existing configuration is migrated automatically as `Mis dispositivos`. This prepares
sharing a selection of notes with another person without mixing it with the profile of one's own
devices; what remains pending is improving the invitation and profile exchange flow.

### Shared profile code — DONE (2026-09-13)

The shared profile can include the note selection together with the sync key. This way,
another user can receive just those notes even if they do not yet have them locally. The code does not include
the URL, the folder or the server token, which are configured separately.

## 6. Logo

The current concept (three colored tabs stuck to the right edge, cut off by the edge) is
correct and stays: it literally draws the product and uses the real palette.

### Brand decision (2026-09-13, closed on 2026-09-15)

**Aldune** is adopted as the application's name at every level: visible name, projects and
namespaces (`src/Aldune`, `src/Aldune.Core`, `src/Aldune.SyncServer`), local data, executable,
installer, documentation and sync deployment.

The physical name of the server's Docker volume is configurable with `ALDUNE_SYNC_VOLUME` (see `SYNC.md`).

The current icon is also kept: the three staggered cards directly represent the dock and
connect with the real palette of the notes. A complete redesign will not be done before validating the product
with real use.

Before a commercial release or charging for the application, a final check of
brand, domains and stores will be made, because at least one very similar name exists, **FanNote**. If the name
changes, the icon, the installer and the public resources will be redesigned together, instead of changing only
the logo.

The problem is one of small size: at 16 px — tray, taskbar, Alt+Tab, which is where it is seen most
— three thin bars with gaps blur together and end up reading as a generic list icon. The
solution is the standard one in icon design: **a dedicated variant for 16 and 32 px**, with fewer
and thicker bars, instead of rescaling the 256 one.

## 7. Launch visual polish

Active block after closing the main functionality. The current identity is kept and
only the details that most affect the product feel are reviewed:

1. **Typography**: set a short, coherent scale for titles, body and auxiliary text.
2. **Icons**: use a consistent family and fix sizes and optical alignment in windows,
   dock and tray.
3. **Spacing**: review margins and separation between groups in Settings and Manage notes.
4. **States**: check hover, press, focus, disabled, error and success without introducing
   new forms of interaction.

A complete theme system or a Lite version is not added yet. They will be studied after
obtaining real use and measuring whether a clear need exists.

The first polish already applied includes the shared visual base, the sharpness adjustment of the windows,
the separation of the sync buttons and an ICO with three cards legible at small sizes.

---

## 8. Interaction polish round (session 2026-09-18)

Round requested by the user as a list of concrete points, grouped here into four batches to
cover it step by step. **A single release at the end of the round.**

### Decided scope

- **Light mode: NOT in this round.** It will be studied after this interaction round. The reason is not
  only the volume (it affects all the XAMLs and the `Aldune*Brush` resources, today designed for a dark
  background): point 7 of "Launch visual polish" already says that no theme system is added
  until there is real use. It remains a firm candidate for the next round.
- **Recovery of a note with a forgotten password: NOT implemented.** It is not an oversight, it is a
  design decision: the note is encrypted with a key derived from the password and Aldune **does not store
  the password**. Any "recovery" requires a second credential (a recovery phrase) that
  has to be presented when protecting the note, and that contradicts the current promise ("if it is lost, that note
  cannot be recovered"). What does enter this round is **warning better before protecting**: the
  password dialog must say explicitly that no recovery is possible. If in the future
  real recovery is wanted, the path is the recovery phrase stored separately, never a back
  door in the encryption.

### Batch A — Windows and legibility (in progress)

1. **The notes manager and Settings stayed behind the notes.** Cause: `Topmost="False"` in
   `NotesManagerWindow`, `SettingsWindow` and `SyncConflictsWindow`, while the notes and the docks are
   `Topmost="True"`. A normal window always ends up below, so you had to click twice.
   **Done:** the three become `Topmost="True"` and are opened with `AppCoordinator.RaiseAppWindow`, which
   also moves the notes off the top layer while they are alive (the same mechanism as the dock menu,
   now with a counter so that two owners do not step on each other) and brings the window to the front.
2. **Minimizing a note made its tab disappear from the dock.** `RefreshOpenState` hid the
   tab just because the window was open, without looking at `WindowState`. **Done.**
3. **Unreadable text selection.** `ApplyColor` painted the selection with the **opaque** ink, so
   a solid block of the text color showed. **Done:** translucent wash of the ink.
4. **Little air between the note's scroll bar and the text.** **Done** with an implicit style of
   `ScrollBar` in the `TextBox` resources: the control's `Padding` would not have worked, because the
   bar lives inside the internal `ScrollViewer` and the padding shifts text and bar at the same time.
5. **Notifications always appeared on the primary monitor.** `UpdateAvailableWindow` used
   `SystemParameters.WorkArea`, which is always the primary one, ignoring which screen it was requested from.
   **Done:** `MonitorEnumerator.MonitorContainingCursor` resolves the cursor's monitor (which in an
   action triggered by a click is the originating one) and the notice is placed in its bottom-right corner.

### Batch B — Interaction (in progress)

6. **Dropdowns that are not toggles.** The note window's actions panel tries to close when the
   trigger is pressed again (`OnMenuPreviewMouseDown`), but it **does not work**, and the cause is
   verified in the WPF source (`Popup.cs`): with `StaysOpen="False"` the popup takes mouse
   capture (`Mouse.Capture(_popupRoot, CaptureMode.SubTree)`) and is dismissed **on mouse-down** — there is
   a comment in WPF itself about dismissing nested popups "one on MouseDown and another on MouseUp".
   That is: when the click reaches the trigger, `Popup.IsOpen` is ALREADY `false`, so querying it there
   always answers "closed" and the handler reopens. The four right-click menus of the dock
   (`OpenAllMenuPopup`, `DockViewPopup`, `NewNoteMenuPopup`, `TabMenuPopup`) and the two of the manager
   (`TagManagerPopup`, `TagEditorPopup`) have the same problem.

   **Done.** Reusable helper `PopupToggle` (`Windowing`), which decides from the
   `Popup.Closed` event instead of from `IsOpen`: it arms itself only if, on closing, (a) the pointer is over the
   trigger — **checked by coordinates**, not with `IsMouseOver`, which with the popup's capture
   active no longer reflects where the cursor is — and (b) a mouse button is pressed. Those two things
   together are the signature of "the trigger itself closed it"; the other closings (choosing an option,
   `Esc`, the tab menu's automatic close) meet neither of the two and do not swallow the
   next click. The trigger queries `ShouldConsumeOpen()` on its mouse-up/click. The tab menu's
   trigger is variable (each tab is its own), so `SetTrigger` is called on each
   opening; the rest use the fixed button in their footer.
7. **Wheel / autoscroll under the cursor.** Finding: `OnTabsPreviewMouseWheel` was not the underlying problem.
   The real problem is that the dock is never activated (`WS_EX_NOACTIVATE`, `ShowActivated="False"`)
   and Windows delivers `WM_MOUSEWHEEL` to the window **with focus**, trusting that its `DefWindowProc`
   will forward it to the window under the cursor; several apps swallow it and the dock does not receive it.

   What the user finally asked for was not the native wheel but the **classic autoscroll**: a
   middle click sets an origin point and, from there, the view scrolls by itself in the direction of the cursor
   (up, down and, if the surface allows it, left and right), faster the farther away.
   **Done:** `AutoScrollManager` (new, `Windowing`) implements it for the three surfaces: the
   dock's fan, the manager's list and the note body. It polls the cursor with Win32 — with the
   WPF API it would freeze as soon as the cursor left the window, which is normal in this
   mode —, uses no global hook (the middle click does reach non-activatable windows) and turns off
   with another middle click, any other button, Esc, the wheel or moving the bar by hand. The origin point's glyph
   is the shared resource `AutoScrollOriginGlyph` (`App.xaml`), with `x:Shared="False"`
   so that two windows do not fight over the same visual instance.
8. **Empty task checkbox: clicking beside it ticks instead of typing.** **Done:** the click is accepted
   only inside the same box that is highlighted on hover (`visualRect.Contains`), instead of
   counting any point from the start of the line to the end of the prefix.
9. ~~**Dock tag assigner different from the manager's**~~ **Done** (all three already use `TagAssignmentPanel`): there were two implementations
   (`OnTabMenuTagsClick` + `DockTagAssignmentItems` versus `TagChoiceItems` + `OnSaveTagsClick`);
   they are unified into the manager's. **Pending.**
10. **Templates distribute in opening order**, not in deck order, so the note that lands
    in each cell looks random. **Done:** `ArrangeOpenNotes` sorts the windows by their index in
    the dock view (`NotesForCurrentDockView`).

### Batch C — Design and UX (done)

11. **The note's 3-dot menu**: only **redistribution and grouping** of the buttons; no switchable simplified/advanced
    mode is added. **Done.** Four blocks by intent: writing (task, bullet, with
    their shortcut in view — it remains the only way to discover Ctrl+L without going to Settings), reminder,
    note state (color, pin with its explanation, protect, tags) and, separated at the end, what
    saves outside or destroys (export, restore size, archive, trash). Same criterion as `impeccable`'s Distill
    (group by intent, hierarchy in four blocks) applied by hand to the XAML.
12. **The dock dropdown closed by itself when the mouse moved away** (550 ms timer). **Done:** the tab
    menu is now fixed — it is closed only by a click outside (native to `StaysOpen="False"`) or by choosing an option.
    Out went the timer, the flags and the four enter/leave handlers, in both the `code-behind` and the XAML.
13. **The layouts in the "open all" menu** — grid, columns and cascade — remain the
    only three, and **"Restore original positions" is removed**: the user did not know what it did and the
    same job is covered by remembering the position when opening each note. In addition, the cascade sticks to the edge
    of the dock (before it ended up at the top right, far from the fan).
    **Business novelty**: a new note **enters the chosen layout**. When opening any note
    with a layout other than Normal, the open ones on that screen are redistributed so that it
    fits — so "open all in columns" is a state of the desktop and not an action of one instant, and
    the notes opened afterwards fit in where they belong. With Normal (factory) nothing is touched.
14. **Native copy/paste/cut context menu** with the app's dark-mode look.
    **Done:** implicit `ContextMenu` + `MenuItem` styles in `App.xaml`, with the same surface as the
    rest of the floating elements (Ground + corners + shadow) and the same rows as `NoteMenuItemStyle`. The commands
    remain the native ones, with the translation of the user's Windows, not an invented one. It is picked up by
    any `TextBox` (notes, Settings, manager, reminders) without touching each window.

### Batch D — Closing (done)

15. **Trackpad**: reviewed. A precision trackpad does not send its own gestures to a desktop app —
    it sends wheel (and horizontal wheel) with small, consecutive deltas, so "trackpad interactions"
    is not a list of gestures that can be enabled: it is **how the wheel is interpreted**. New setting
    `AppSettings.TrackpadGestures`: with it on, the dock's fan uses a step proportional to the delta
    (a two-finger gesture moves what it should) instead of the fixed step of one mouse notch per event.
    `TouchpadDetector` (`Interop`, best-effort via registry) decides whether the setting is available: without a
    precision trackpad, Settings leaves it **disabled and off**, and explains why. **Not
    verified on real hardware**: the development machine has no trackpad (`PrecisionTouchPad` does not
    exist in the registry), so the only thing checked is that detection answers "no" and that the
    switch comes out locked with its explanation. With a mouse the path does not change (deltas ≥ 120).
    The middle click (autoscroll, point 7) is the trackpad alternative for scrolling without a wheel.
16. **Trackpad and shortcuts, documented**: `Ctrl+Alt+H` (new) and the tray switch in the quick
    help. See "Side effects of the screen turning off" for the third point of this batch.
17. **Documentation and version**: `STATUS.md`, this roadmap, quick help and `README` up to date; version to
    **0.10.0** (new functionality) in `Aldune.csproj` and here.

### Fixes on v0.10.0 (same session, after the user tried it)

- **Autoscroll was slow** (33 ms tick and low cap): it goes to 16 ms with a higher cap.
- **The note's "⋯" did not close when pressed again**: the toggle (`PopupToggle`) was declared but
  **never instantiated** in `NoteWindow` — the field stayed `null` and the guard was never evaluated.
  It is now created in the constructor.
- **Cut/Copy/Paste came out white**: the implicit `ContextMenu` style did not set `Foreground`, and the
  `MenuItem`s inherited the system one (black on dark). Now the menu itself brings the ink tone.
- **Tray hover**: `DarkMenuColors` paints the hover flat and without gradients, including that of the
  checked row ("Hide the dock").
- **Monitor bug on screen turning off** (seen in the test): the "before" signature was computed in
  the change event, when the driver had already removed the screen from the list, so it stored the broken
  state. It is now recorded after each good rebuild, which is the only moment in which the app
  has monitors and docks aligned.

### Review on 0.10.0 (second pass; published as 0.10.2: unification)

- **The monitor cycle kept stopping after the first rebuild**: `_preChangeMonitorKey` was set
  **inside** `BuildDocks`, so the tick's comparison against the freshly built signature was
  always true and the cycle was considered finished without waiting for the screen to come back. Now the
  criterion is one of **stability**: it rebuilds as long as the signature changes from one tick to the next, with a
  minimum wait of 3 s from the change (for slow drivers that publish the screen in 1-2 s) and a cap
  of 20 ticks. The initial debounce drops from 600 to 400 ms, which was what was noticed as "takes longer than
  it should".
- **The tray always said "Hide the dock"** with an inverted checkmark: now the
  text says the action — "Hide the dock" when visible, "Show the dock" when hidden — and the checkmark has
  gone away.
- **The cascade and the templates go in deck order**, extracted into `OpenWindowsInDeckOrder()` so as not to
  duplicate it: before, the cascade used the dictionary's opening order.
- **Autoscroll holds the fan open** while it is active: without that, moving the mouse away to
  scroll faster folded the dock under the cursor.
- **Out went the dead `OnMenuPreviewMouseDown`** of the note (and its hook in the XAML): since
  `PopupToggle` decides on Click, that handler was a no-op that confused more than it explained.
- **Unification of two parallel sessions**: with two sessions working at the same time on the same tree, the
  other session implemented the trackpad on its own (`TouchpadDetector`, `AppSettings.TrackpadGestures`,
  checkbox in Settings) and published `v0.10.1` with the "open all" menu **without** "Restore
  positions" (removed, not renamed). On merging, its decision is respected and what was
  left over is removed: `OnRestoreOriginalPositionsClick`, `Coordinator.RestoreNotePositions`,
  `RestoreOpenNotesToMonitor` and the text `Strings.RestoreOriginalPositions` — all without references
  from anywhere. **If the user misses the restoration, it is recovered from git** (the code
  remains whole in the history) instead of leaving half of it dead.

### Fixes on 0.10.2 (published as 0.10.3)

- **The note's "⋯" reopened when pressed with the menu open**: `NoteWindow`'s `PopupToggle`
  never received its trigger (`SetTrigger`), so it could not know that the closing had been caused
  by the button itself. The dock's and the manager's toggles did set it; this one did not.
- **The copy/paste menu was still light**: a TextBox's native menu is an internal subclass of
  `ContextMenu` and WPF's implicit styles match only by exact type. `DarkTextContextMenu` gives
  each TextBox its own `ContextMenu` (Cut/Copy/Paste with the native commands) that does receive
  the dark style from `App.xaml`.
- **Cascade next to the dock with room for the fan**: `NoteCascade` (Core, with tests for the 4
  edges) starts next to the unfolded dock's rectangle (`EdgeGeometry.WindowRect`), not the edge.
- **Order of the templates**: opening several notes triggered one layout per note (`arrangeAfterOpen`), which
  stepped on each other; now the batch does just one. Grid/columns are filled by columns with the
  dock on a side and by rows with the dock at the top or bottom.
- **The note's scroll bar** with the note's ink (thin thumb, transparent track).
- Out went the orphan "Restore positions" comment above `OpenSettings`.

### Adjustments on 0.10.3 (published as 0.10.4)

- **"Cascade next to the dock" is now a template that is remembered** (`NoteLayoutTemplate.DockCascade`,
  at the end of the enum because it is stored as a number). Before, only Normal, Grid and
  Columns were remembered: after cascading, closing and pressing the button with a left click, the notes came out in another
  layout. Now the left click repeats the last one chosen, cascade included, and a loose open note
  fits into it.
- **Cut/copy/paste menu**: it opens explicitly to the right and below the pointer
  (`PlacementMode.MousePoint`) and its template leaves 10 px of air for the shadow, which came out clipped.
- Animation review: they are now coherent (150-220 ms, ease-out, scale 0.95 + fade) and nothing
  is touched. The dock's dropdowns open toward the inside of the screen depending on the edge, on purpose.

### Hiding the dock (requested during the round)

`Ctrl+Alt+H` and a row in the tray menu ("Hide the dock", with a checkmark while it is hidden) hide
the dock without closing it and bring it back. Reason: there are full screens that Windows does **not** report as
such (full-screen video inside a browser, especially borderless), so the full-screen
detection does not cover them and the dock stays on top. The shortcut is essential in that case because
the tray is not in view either. `AppCoordinator.DocksVisible` is **not saved**: it is an action of one
moment, not a preference, and persisting it would mean one day starting without a dock and not remembering why.

### Side effects of the screen turning off (reported bug)

**Symptom**: with two monitors, turning one off moves its dock to the other screen; when turning it back on, the
dock stays where it was moved instead of returning.

**Cause**: when rebuilding the docks after a `DisplaySettingsChanged`, Windows publishes the list of
monitors in several steps and `App.OnRebuildTick` stopped at **5 ticks** (≈3 s) no matter what.
If the returning screen takes longer to appear in the list, the last rebuild is done with a single
monitor, that dock is created on the wrong screen and there are no more attempts.

**Fix**: the stop criterion becomes "the set of screens is already the one from before the change"
(signature by device name and size, `MonitorSignature`) and only the cap of 12 ticks cuts off a permanent change.
While the set remains different it keeps rebuilding every 600 ms, so the returning screen
recovers its dock by itself.

**Pending verification by the user**: turning the screen off and on again with the app open.


### Decisions made with the user (2026-09-18)

- **Notifications**: they should appear on the screen they were requested from and never on the other. From the
  tray, the cursor's.
- **Mouse wheel**: all three — dock (all four edges), manager lists and note scroll.
- **Templates**: the cell order follows the **deck order**, not the opening order.
- **"Restore original positions"**: **both things** are done — the button now places all
  the notes in a cascade next to the dock, and the restoration of the saved positions is kept.
- **3-dot menu**: only **redistribution and grouping** of the buttons; no switchable
  simplified/advanced mode is added.
- **Air between scroll and text**: 8 px (to be validated in the app; it is a constant).
- **Copy/paste/cut menu**: it is re-templated with the app's look.

### Adjustments on 0.10.4 (published as 0.10.5)

General audit (bugs, security, visual/UX, quality) requested by the user, followed by a round of
fixes. Full technical detail in `docs/STATUS.md` → "General audit + sharpness and
cascade fixes".

- **The copy/paste menu (and the other floating panels) no longer comes out blurry** on monitors with scale
  >100%: the `DropShadowEffect` forced WPF to rasterize the text together with the shadow into a bitmap of
  fixed resolution. The shadow was separated from the content in `ContextMenu`, `ToolTip`, the popups of the dock
  and of the note, and also in the fan's cards (title/preview of each tab).
- **"Unfold all" no longer goes back to cascade** if a note was moved by hand: moving a note turns off the
  active automatic template.
- **Cascade next to the dock**: with many notes they no longer overlap completely on top of each other — the step
  is compressed instead of being clamped to a fixed cap, so something of each note always peeks out.
- **Accessibility**: `AutomationProperties.Name` on the icon buttons that only had a `ToolTip`
  (dock, note, settings, notes manager, color picker).

### Adjustments on 0.10.5 (published as 0.10.6)

- **The cascade kept reappearing when moving a note by the normal header** (not by the grip): the notice
  that turns off the automatic template was only fired from the grip (`OnGripMouseDown`); moved to
  `OnLocationChanged`, which sees the drag wherever it comes from. Detail in `docs/STATUS.md`.

### Adjustments on 0.10.6 (published as 0.10.7)

- **"Unfold all" kept centering the notes in a cascade** even though the template was already
  `Normal`: `OpenAllNotes` rearranged ALL the notes in a diagonal centered on the screen for any
  template, `Normal` included, overriding each one's just-restored position. Now `Normal` does not
  rearrange anything — each note stays where it landed on opening. Detail in `docs/STATUS.md`.

---

## 9. Mobile client and other operating systems (2026-09-28): Android decided; macOS, Linux and web, not

A study of the port to Android and iOS was requested ("something different: more about managing the computer's notes and
jotting something down than imitating the dock") and then a review of what to do with the systems that are missing. Everything was left
**documented and unimplemented**, so it can be picked up later:

- **[`MOBILE_PORT.md`](MOBILE_PORT.md) — Android and iOS** with .NET MAUI sharing `Aldune.Core` (the
  850 tests and the whole sync format are valid as they are). It includes what is reused and what is not ported (dock,
  fan, note windows, per-screen position, tray, shortcuts), the proposed mobile product
  (deck widget, quick capture, notifications with actions, biometrics), the two prior changes
  needed on Windows (QR pairing and, optionally, a sync server inside the PC itself),
  phases and costs (Apple $99/year, Play $25, **a Mac is mandatory for iOS**) and the pending decision to
  **raise the format to 5** if one day reminders have to travel between devices (today they do not
  travel).
- **[`DESKTOP_PORT.md`](DESKTOP_PORT.md) — macOS, Linux and web.** Conclusion: on macOS the concept **already
  exists and is taken** (the apps that inspired Aldune) and the dock mechanics would have to be redone in
  AppKit; on Linux, GNOME (Mutter) does not support `wlr-layer-shell`, so a dock on the edge would be
  a desktop extension, not an application. What pays off from those systems is the **Core**, not the
  dock: a desktop client **without a dock** (Avalonia) the day someone uses it. The sync
  server already runs on Linux. The web, discarded.
- **The two systems that are genuinely missing, and are cheap** (also in `DESKTOP_PORT.md` §9):
  **Windows on ARM64** —today it installs and works emulated, because the installer uses `x64compatible`, which
  includes Windows 11 ARM; publishing a native `win-arm64` is one more RID, no new code— and the **server on
  ARM** (Raspberry Pi, NAS), which already works because the .NET image is multi-architecture.

**Decided on 2026-09-28: Android first, on an Android tablet, without publishing at first** (the user
has an iPhone and an Android tablet). The reasons and the design consequences are in `MOBILE_PORT.md`
§0.1: €0 in licenses, no Mac, a real device to test on, and the tablet as the design reference.
iOS is parked until there is a Mac or until the priority is capturing from the pocket, and that does not
change anything of what has already been decided because the client is one MAUI project plus two Swift extensions.

## 10. Customizable sounds — DONE in 1.7.0 (without its own volume or an "error" event; off by default)

Today the app makes no sound anywhere (the `AppDialog` notices are "silent" on purpose) and the
"Vintage paper" mode with sounds is still not done. Assessed when planning block 2 of the looks:

- **Worth it** because it fits the retro looks (a terminal's beep, XP's "ding") and
  with a reminder notice that today is only seen. **It does not go into 1.5.0**: it is a separate block and the
  looks are already loaded.
- **Proposed design**: fixed events (reminder due, sync finished or failed, new
  note, note archived, error), each with "no sound" / app sound / the user's own `.wav` file
  chosen by the user; general volume; **off by default** (an app that lives on a screen
  edge must not make noise without being asked). Each look can *propose* a set of sounds, just as it
  proposes a note theme (asking, without changing it by itself).
- **Logic in Core with tests** (`SoundSettings`: event → source, validation of the path and the format,
  missing path = silence, never an error). Playback with `System.Media.SoundPlayer` or
  `MediaPlayer`, from the WPF layer, never blocking the interface; a `.wav` that cannot be read is
  ignored and noted in the diagnostics log.
- **Things to decide then**: whether the sounds travel in export/import of settings (the app's
  do; the custom files do not, they belong to each machine, like the chosen screen), and whether to respect Windows'
  "do not disturb".

## 11. Markdown linked to files (user requirements, 2026-10-03; DONE in 1.6.0, phase F1)

For the Markdown spec (phase F1 of a plan removed from the tree), what the user
asked for, verbatim:

- **Open a `.md` that is already in any folder of the machine** with the application, and have it **stay in the
  dock pointing to that place**: the note *is* the file (it is edited there and the file changes), not a copy.
- **Choose per note whether it enters sync or not.** Reason: the same file may be in the same
  place on the other machine (the laptop), for example a subject's notes on `Z:` (network
  volume), or may not be.
- The rest of phase F1 stands: writing in Markdown, photos, and local files.

Questions the spec will have to close (not decided): what travels through sync when the note is
linked (the path, which differs between machines, or just the content, or a path relative to a named "root"
that each machine assigns: `Z:\apuntes` here, another letter there); what happens if the file is missing,
moved or on a disconnected network drive; simultaneous editing of the file by another application
(watching for changes, conflict); and how it looks in the trash and in the archive. It changes the data format
forever: deep brainstorming before writing code.

## 12. Mobile client: after everything above (user decision, 2026-10-03)

Order: looks blocks and the 1.5.0 release, Markdown, MSIX and, **afterwards**, the mobile client
little by little, so as to **be able to edit notes from the phone without depending on the computer**. What has already been studied and
decided (Android first, on the tablet, without publishing at first) is in `docs/MOBILE_PORT.md` and in
§9 of this file; it has to be reread and reviewed against whatever has changed by then (file
linking, sounds, sync format).

## 13. What could make someone pay (2026-10-25)


Prior decision (unchanged): stay GPL-3.0 and **validate with 3-5 people before selling**. Ideas, from most to least clear:

1. **Easy pairing (chosen by the user to implement, 25-10-2026).** Today you paste `aldune-profile-v2:…` and type the token separately (`SYNC.md`). A QR of the profile code reduces it to scanning. This revises the decision of 21-10 («QR after F8»): the user prefers to do it now. **Restriction that does not change:** the token never travels in the QR (that code is also used to share a note and would hand over write access to the whole store). Design pending: how the token arrives (typing it by hand, or a short second step), camera on Android (CameraX + ZXing) and generation on Windows (QRCoder). Without changing the sync format (4) or the `aldune-profile-*` prefixes.

   **Decided on 25-10-2026 (spec and plan missing; pending, not started):** the token is still typed separately (no changes to server or format). Windows and Android show the profile code's QR; Android scans it. Proposal: CameraX + ZXing (free, F-Droid compatible; the ML Kit scanner needs Play Services) on Android and QRCoder (MIT) on Windows. If the profile carries specific notes and the code does not fit in a QR, the screen warns about it and offers to copy the code as today. Next step: write the spec in `docs/specs/` and, with its approval, the plan with tests first.
2. **Import Markdown and one-click backup.** Cheap; removes entry friction (see `PENDIENTES.md`).
3. **Microsoft Store (MSIX).** The signing and not going through SmartScreen weigh more than any feature; SignPath already rejected the project. Postponed until Android is closed; to sell it is probably the most necessary.


**Mobile parity, retro looks on Android — DONE (2026-10-25).** XP light and dark, Telecommunications light and dark, Bash and Phosphor, with their customizable colors, the Bash palettes, the suggested note theme and a monospaced font where applicable; without the Windows "skin". Palettes identical to the PC's, fixed by vectors (`tests/vectors/text-color.json`). Tested on the Pixel with the debug version. The widget too (accent on the title and the "+", and monospaced font in Bash, Telecommunications and Phosphor). Published in `android-v0.1.0-rc5`, `rc6` (widget accent) and `rc7` (widget font); installed on Pixel and tablet. Spec: `docs/historia/specs/2026-10-25-aldune-android-aspectos-retro-design.md`.
