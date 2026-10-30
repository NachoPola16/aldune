# Decisions of 2026-10-26

Taken with the user after Android rc8 (published on 25-10) and Windows 1.7.11. None of this is implemented
yet; this document is the order of work. If something is discarded here, it does not have to be
investigated again unless the reason changes.

## Windows settings that Android does not have

| Setting | Decision | Reason |
|---|---|---|
| 5. Hide done tasks (`AutoHideCompletedTasks`) | **Do it**, as *hide*, not delete: the tasks stay in the note and a “show done” switch reveals them | That way it stops being destructive and no long design is needed |
| 7. Simplified mode (`SimplifiedMode`) | **Done on 27-10** (see `DECISIONS-2026-10-27.md`) | It was asked for after reviewing what it does on Windows: it is a switch in the Settings window, not another app |
| 8. Sounds per event | **Discarded** | On Android sound is handled by the system's notification channel |
| 9. Preview in the list | **Discarded** | The phone already always shows it |
| 11. Custom themes (`CustomThemes`) | **Postponed** | The built-in themes and the free color are enough; it is picked up again if someone asks |

## Logo

**Closed on 26-10:** the final symbol is the **“A” made of three notes** (candidate G), a single drawing from which all the
formats come (`scripts/branding/generar-iconos.py`). It replaces the three tabs with a bar, which in a circular mask looked
like a rectangle stuck in a circle. Candidate C (stacked notes) is kept as an alternative. Detail, colors, usage rules and
what was discarded: [`BRANDING.md`](BRANDING.md).

## Widgets and bubbles (Android, 26-10)

- **More widgets in the picker:** besides the 4×2 deck, there is now the **2×2** deck (compact), the **4×4** deck (large) and a
  **1×1** “New note” button (the symbol; one tap opens the editor). The three deck sizes reuse `DockWidget`, which already adapts to
  its size and repaints together (Glance keeps the set of receivers of each class). All can be resized except the 1×1.
  **The 4×1 was discarded:** between header and padding no useful strip fits.
- **Bubble:** **there is nothing to build.** Android 17 lets you turn any app into a bubble with a long press on its
  icon (official Android 17 blog); on the Pixel it works with rc9 as is: multi-window, the activity is
  resizable by default (`targetSdk` 36) and `adjustResize` is already there. A bubble of our own from the capture notification
  (`BubbleMetadata`) is discarded: the system already covers the case and it would force us to maintain another embedded activity.
  Not tested yet: the app in a very small bubble window (tablet or foldable).

## Windows 1.7.12 (small release)

- Copy the path and open the original note also from the dock, on notes opened from a folder
  (today it is only in the note's “⋯” menu).
- Import a `.md` as a normal note (today it is only opened, linked or dragged).
- Publishing is an outward-facing action: it is confirmed with the user first. Afterwards, update the sync server
  (see `RELEASING.md`).

## Markdown

- Import `.md` as a normal note: **do it** (goes in 1.7.12).
- Underline and typography of the selection: **discarded** (it requires rich text; see `ROADMAP.md:424-439`).
- Pasting photos: still discarded.

## Pairing by QR

**Revised on 27-10:** it is being done; see `specs/2026-10-27-qr-emparejamiento-design.md` (it fits in a single QR and splitting it is discarded). Original text:

**Not done for now.** The `aldune-profile-v2:` code carries key, scope, noteIds and URL; with many notes a single QR
comes out too dense and it would have to be split in two. The current pairing (paste the code and type the token
separately) works. It is picked up only if validation with 3-5 people shows it is an obstacle. In any case,
**the token never goes inside the QR**.

## Business

- It stays **GPL-3.0**.
- Not sold yet: first validate with 3-5 real people.
- **IzzyOnDroid and F-Droid are picked up again** (GPL-compatible, free visibility for that validation). It replaces the
  pause of 24-10.

## No changes

- Store listing screenshots: after closing the logo work.
- The 3 minor F7 probes: considered done.
- Dock that sometimes does not hide: stays as it was (repaint after 10 s and `KeepStripOnTop`).
- MSIX / Microsoft Store: still postponed.

## Order of work

1. Hide done tasks on Android (decision 5), test first in `android/domain`.
2. Windows 1.7.12: dock + import `.md`, with tests in Core.
3. Confirm the logo on the phone → screenshots → IzzyOnDroid/F-Droid.

## Done on 26-10 on Android (local commits, not pushed)

- Hide done tasks (setting + “Show N done tasks” button in the editor; visual only, the text does not change).
- Bottom margin of the list so the “+” does not cover the last note.
- Undo and redo in the editor bar (the history is cleared when the note loads, so the first “undo” does not empty it).
- “Done/total” counter in the editor header, with a description for screen readers.
- Short vibration when ticking or unticking a checkbox.

## To test with the phone connected (none of this has been seen on screen)

1. Hide done tasks: that they disappear after the deadline, tapping checkboxes with hidden lines above, typing next to a hidden line, the button that shows them.
2. That the “+” does not cover the last note and that the margin does not look odd with few notes.
3. Undo/redo with the Samsung/Gboard keyboard and with a third-party one; that after opening a note “undo” is disabled.
4. Counter and vibration: that they are not annoying; TalkBack reading the counter.
5. In the same pass, look at accessibility (large font size, TalkBack, 48 dp touch targets) and what feels copied from the PC.

## Ideas to look at (undecided)

- Phone: shadow/elevation on the dragged note; a search bar that hides when scrolling down; batch actions (select several).
- From phone to PC: “done/total” counter in the note; “see done tasks” as hide without deleting on Windows (today it deletes the line).

## Windows and server security audit (26-10) and 1.7.13

Read-only security pass: nothing critical or high. Fixed in 1.7.13:

- **WebDAV (medium):** an `href` in the listing pointing to another host received the user and password. Now only its path is used against the configured server and whatever falls on another host is discarded (`WebDavSyncTransport.ResolveHref`, with tests). Android already did it right.
- **Protected note password (medium-low):** minimum of 4 to 8 characters for new ones; unlocking existing ones still has no minimum.
- **Sync server (low-medium):** rejects with 400 anything dated more than a day in the future (it used to block the note forever).
- **Linked files:** read with a cap (MaxBytes + 1) instead of loading the whole file, and “Open in its editor” only for text extensions.
- **Workflows:** actions pinned by commit SHA (with the tag in a comment) and `no-new-privileges` in the three server composes.

Deliberately postponed (noted in PENDIENTES):

- Server container as user `app` instead of root: the permissions of the already created `/data` volume have to be migrated; not touched without testing it on CT 103.
- Port published on `0.0.0.0` by default: the real deployment depends on it (NPM on another machine); it is already documented and the app warns. The default is not changed.
- No limit on failed token attempts, `GET /objects` reads all objects on every call, no size cap on server responses to the client, no object quota, `release.yml` with `contents: write` in the whole job, no NuGet lockfile.

## Result of the test on the Pixel 10a (26-10, debug app, already uninstalled)

Checked on screen: “done/total” counter in the header (3/5 → 4/5 when ticking), hide done tasks (with 2 of 3 expired they hide and “Show 2 done tasks” appears; the button reveals them and hides them again), tapping a checkbox with hidden lines above ticks the right task, undo and redo (disabled on opening the note, undo brings the checkbox back, redo becomes enabled), margin below the list (with 13 notes the last one stays above the “+”), system font at 160 % (no clipping; the editor bar is still scrollable).

Not checked: the vibration when ticking (not visible in screenshots), TalkBack, third-party keyboards, and the real 1 h/1 day/1 week deadline (it was tested by moving the stored times forward).

Still to look at: the “See N done tasks” button sits on the left and is not very prominent; the editor bar is cut on the right (“⋯” half visible) although it scrolls, perhaps a fade or fewer visible buttons.
