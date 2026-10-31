# Decisions, 2026-10-31

Work list: the pending changes for Windows 2.2.0 and Android 1.1.0 (one release of each, nothing published in between). Each item is "do" or "do not" with the reason.

## A. Bugs

### A1. Trash (Android) — DONE
- **Swipe stops working after restore + Undo.** Reproduced on a Pixel 10a with a Compose instrumented test (`TrashSwipeTest`): the row's `SwipeToDismissBoxState` came from `rememberSwipeToDismissBoxState`, which uses `rememberSaveable`, so the state was restored already dismissed. It is now built with `remember(item.id, item.state)`, always at rest. Windows has no equivalent (the manager uses buttons, not swipes).
- **Menu / "Open note" on a note that is gone.** The long-press menu and the delete confirmation kept a stale `NoteListItem`. They now close when their note leaves the list and always show the live item. Not reproduced by hand (it needs a finger); fixed defensively because the stale reference is evident in the code.
- Known and left alone: `confirmValueChange` calls `onDelete` several times per gesture (it returns `false`, so the state retries); it only sets `deleting`, so it is idempotent.

### A2. Task checkboxes (Android) — PARTLY DONE
- **Wrong row ticked.** Reproduced on the Pixel with a 60-task note scrolled to the end: the counter went up but none of the visible rows was ticked. Cause: the tap position was resolved against the text layout without adding the field's scroll (the task drag does add it). Fixed in `NoteEditorPane.kt` (viewport → layout coordinates at tap time, so a scroll during the one-frame delay does not matter). Eight rapid taps now tick exactly the eight rows tapped.
- **Cursor lands beside a checkbox and ticking stops (only the cursor moves).** Reproduced the mechanism on the Pixel: `getOffsetForPosition` returns the nearest character boundary and only `offset` and `offset - 1` were tried, so a tap in the gap between the box and the first letter (about 12 px wide) resolved to the text and put the cursor there. With a collapsed cursor just after a box, the cursor handle (a teardrop under the caret, with a 48 dp touch area) sits over the *next* row's checkbox and swallows its taps (two taps on it ticked nothing). Fix: the target is now the box plus the space after it, the whole line high (`toggleCheckboxAtTap`, covered by `CheckboxTapTest`); the first letter of the task text still only places the cursor. The handle itself cannot be intercepted (it is a popup); with a cursor placed by tapping the text, tapping the row below's box under the handle still fails, which is standard text-field behaviour and left alone.
- Windows: not affected (a WPF text box; `TaskLines` in Core is only text logic and was not involved).
- No automated regression test: the editor is too coupled to the view model to host in a Compose test. The probe is documented in `ANDROID_PROBES.md`.

### A3. Settings tab scroll (Android) — DONE
One scroll state for all tabs, reset to the top when the tab changes (not when the screen is recreated by a rotation). Verified on the Pixel: scrolled down in Appearance, switched to Notes, it starts at the top. Windows: its Settings pages are separate; not affected.

### A4. Dragging a note to the edge of the list (Android) — DONE
While a note is dragged, a per-frame loop scrolls the list when the dragged row's centre is within 72 dp of the top or bottom, faster the closer it is, and adds the scrolled distance to the drag offset so the row stays under the finger. Verified on the Pixel with 16 notes: the last note was dragged up past the visible rows (position 16 to 5). Windows: the dock/manager reorder by dragging inside a list that fits; not changed.

### A2b. Checkbox tolerance and multi-line tasks (Android) — DONE
Requested after A2: a left-handed thumb sometimes missed the checkbox, and a task that wraps put its continuation under the checkbox.
- **Target.** The checkbox target is now everything from the left edge of the field (the 16 dp margin counts) to where the task text starts, over the full height of the line: about 37 dp wide by one line high, from a ~14 dp box before. The tap detector moved outside the padding for this. The text of the task still only places the cursor. Verified on the Pixel: taps at x = 10, 25, 40, 63 and 95 px all toggle the row; x = 140 (text) does not. Not done: a taller target. Lines are adjacent, so a taller zone would steal taps from the neighbouring row; the alternative is a larger line height for lists, left as an idea (it also moves the ruled/grid paper, which is tied to the line height).
- **Hanging indent.** The field's text style carries `TextIndent(restLine = width of "checkbox + space")` when the note has tasks, so continuation lines start under the task text. Two per-paragraph attempts were discarded: a paragraph per task line adds an empty line for every line feed, and putting the feed in its own near-zero-height paragraph made the cursor at the end of a line draw as a tiny dash at the left margin. Limitation: in a note that has tasks, a plain line that wraps is also indented (Android applies the indent to every hard line); an indented sub-task's continuation aligns to the same column as a top-level one, not to its own text. Windows keeps its own layout.

## B. Look and spacing (Android)

### B5. Card borders in the note list — DONE
The outline was not about the theme or the note colour as such: `SwipeToDismissBox` always painted its swipe background (the `surfaceVariant` fill) behind every card, with the same size and shape, and the card's anti-aliased edge let it show as a thin line whose visibility depended on how the note colour compared with it (confirmed on the user's screenshot: ComDig, Seguridad and Redes, which are darker than that fill, had it; the greyer ones did not). The background is now drawn only while the row is actually swiped. Separately, a card whose face is within a WCAG contrast ratio of 1.25 of the app background (Baloncesto and Micro on the screenshot read as loose text) gets a 1 dp `outline` stroke; the rule is by contrast, not by colour, so it is the same in every look. Windows: not applied; its notes are windows with their own frame, and the manager rows are already separated by the look's borders.

### B6. Sync check in the top bar — NOT DONE (measured, aligned)
On the screenshot the icon's right edge is within 2 px of the cards' right edge, so it is aligned; it only looks inset because the icon is lighter than the cards. Redesigning the bar is not justified. The row's height is kept (48 dp touch target).

### B7. Text margin, right vs left — NOT DONE (deliberate)
`TEXT_END_PAD` is 12 dp against 16 dp on the left on purpose: a wrapped line leaves on average half a letter free on the right, so 12 dp makes the visible gaps oscillate around the left one (documented where it is defined). Equalising them would make the right margin look wider.

### B8. Space around the tag row — DONE
It was about 20 dp above the chips (tabs row is 48 dp tall because of the sync button) and 8 dp below. The chips' own top padding is gone and 10 dp were added below, so the two gaps are about 16 and 18 dp. The tag chips being cut at the right edge with no hint is left as is: it is the standard scrolling-chips pattern and the cut itself is the hint.

### B9. Section header highlight in the options sheet — DONE
The band was the pressed/focus highlight of the full-width header row, starting exactly at the text. The row now runs 12 dp beyond the text on each side (the highlight has padding) and is widened by the same amount so the title still lines up with "Color" and "Reminder". The text buttons ("Choose another colour...", "Export", "Protect") were 12 dp further right than the titles because of their own padding; they are shifted back so all texts share one column. The colour swatches are laid out in balanced rows (8 = 4 + 4, no orphan) and have a 1 dp outline so near-identical greys can be told apart. Checked on the Pixel.

### B10. Landscape — NOT DONE YET
To review on the emulator (rotation) together with the editor changes of A2b; nothing found by reading the code.

## C. Functions

### C17. Menu of the three dots in the editor — DONE
Offset 12 dp to the left: it opened flush against the screen edge.

## Windows

All four measured with a probe of real windows (physical mouse and keys, temporary data), before and after, in XP light and Dark and with "Text size" at 100 % and 150 %.

### W1. The X of the "Sync conflicts" window — DONE
Measured: 3 px too high (dx 0, dy -3). The button style placed its content without centring it, so the 28 x 28 glyph box was drawn at the top left. Fixed in `SyncConflictsWindow.xaml` (centred content presenter): the X is now at 0.0 / 0.0. The other windows with a close X were measured at the same time (manager, settings, note): all within 1 px, so nothing else changed. The minimise dash of the manager stays 2.5 px low on purpose (earlier decision).

### W2. Margins of the manager's button bar — DONE
In the XP bars the manager's header is taller than the buttons (two lines of text), and the buttons were top aligned: 6 px above, 11 below, 8 to the right. They are now vertically centred in that bar (8 / 9 / 8 px; the bar is 45 px tall, so an odd pixel is left) and the left margin of the text went from 12 to 8, so the four sides are alike. Settings was already 6 / 6 / 8 and the note 8 / 7, so they are untouched. The flat looks keep their layout (the buttons sit at the top, 18 px from the edge, because the header is a title block, not a bar).

### W3. The cogwheel looks selected in the manager — FIXED, it was a bug
Not the "Settings is open" signal. After the click, the cogwheel keeps keyboard focus; when Settings is closed (almost always with Esc) WPF gives the focus back to it and, as the last input was the keyboard, draws the app's focus ring (and the tooltip in XP). Reproduced with a physical click and Esc. Now the manager moves keyboard and logical focus to the window itself before opening Settings, so nothing is highlighted on return (checked: the cogwheel no longer has focus).

### W4. Ctrl+F bar in the note — FIXED
At the default size (300 px) the bar did not leave the window, but it nearly filled it: 3 px from the left edge and 26 px from the right, with the text box fixed at 120 px. Now the text box is the only elastic part (40 to 120 px) and the bar leaves the same gap on both sides (25 / 27 px); the counter reserves the width of "No results" so the bar does not jump while typing. Checked at 100 % and 150 % text size.

### W5. "New note" button of the manager (requested later) — DONE
In the header it was the only button with a box among flat window buttons and clashed in every look, worst in XP. Moved to the end of the filter row (Active / Archived / Trash / tags), as a 30 x 30 accent square with a drawn "+". Tried first in the footer: with a label the footer wraps to two lines at the default width, and the footer already holds four action buttons. The filter row's right side was empty, the button sits next to what it acts on, and the header is now only window buttons. Checked in XP light, Dark and Telecom, and at 150 % text size.

### W6. Footer buttons of the manager: right-aligned or spread out — RIGHT-ALIGNED, same width
Kept at the right (actions on the selection go at the end; "Delete", which only appears in the trash, stays alone on the left as the irreversible one) and made all four the same width (`MinWidth` 86): Export and Trash were narrower than Archive and Restore and the row looked ragged. Spreading them over the full width was rejected: they would drift apart from each other and would wrap sooner with a larger text size. Still one row at the default width.

### W7. Manager buttons: text size and colours per look — DONE
- **Text size.** The footer buttons (and every button using `ToolbarButtonStyle`) had no font size of their own and kept the window's fixed one, so with "Text size" at 150 % they stayed small while the rest of the manager grew. They now use `AlduneTextCaption` (12, the same as before at 100 %), which scales.
- **Colours, measured in the 16 looks.** Text on a normal button (4.5+ everywhere: fine). Disabled text (Archive / Restore / Trash with nothing selected) was 2.2 to 3.1 : 1 on its background, hence the faded look. The shared palette is a contract with Android and its vectors, so the base values were not touched: `AppPalette.WithReadableDisabledText` moves `TextDisabled` just enough to reach 3 : 1 against `Surface` and `Ground`, applied by `ThemeManager.Apply` (not in high contrast, where the system colours rule). Still dimmer than normal text. Tests: `ButtonContrastTests` over every look.
- **The accent "+" button had a bug of mine**: it inherited the grey hover of the normal button, so its text was 1.5 to 2.5 : 1 on hover and pressed. It now uses `AccentHover` (5.6 to 13.4 : 1) for both states.

### W8. Symmetry of the dock's button bar — DONE
Measured the gap between the bar's edge and its buttons in every dock edge, round and square corners: **17 px on the left against 7 on the right** (16 / 6 in the top and bottom docks, which also used a padding of 6). Causes: every button carried `Margin="10,0,0,0"`, so the first one had 10 px extra on its left; and the three small buttons were 34 against the 40 of "+", which left 10 px above and below them against 7 around "+" and, with the rounded ends, made the circles not concentric with the bar's ends (the user's guess about the "+" size was right). Decision: the four buttons are 40 (the "+" is told apart by its accent colour, not by its size), the gap goes between buttons (first button has no margin, set after mirroring the order for the left dock: `SpaceFooterButtons`), the gap between buttons equals the padding (7) so the rhythm is the same in every direction and the bar still fits the side docks, and the top/bottom docks use the same padding. Measured after: 7 / 7 / 7 / 7 in all edges and looks (8 in XP, whose border is 1 px thicker). The bar is 18 px wider than before in the side docks.

### W9. Icons of the dock's button bar — DONE
Measured the ink of each icon against the centre of its button in 7 looks (round and square, every edge): all within 0.5 px (half a pixel is inherent to a 13 to 17 px stroke in a 40 px box). With the buttons now 40 px the 15 px glyphs looked small (33 % of the button, 44 % before), so the three glyph icons went to 17 and the "+" to a drawn cross (two 2 px bars in a 16 px box, 0.0 / 0.0 px: as a font glyph at 19 it was 1 px off). The sync button's glyph is reset to 17 in code too (it was put back to 15 by the sync animation code).

## Still pending (end of the first working session, 2026-10-31)

Nothing has been published. Commits are local on `main` (not pushed). Work list for Windows 2.2.0 + Android 1.1.0:

1. **Pinned notes, interface** (Core, Room, mapping, vectors and tests are done): Android (`NotesViewModel.setPinned` -> `NoteService.setPinned`; "Pin to the top of the list" / "Unpin" in `NoteActionsDialog` and `NoteOptionsDialog`
   (`NoteOptions` needs the flag); a pin mark in `NoteRow` (a drawn pin: `material-icons-core` has none); reorder and Up/Down only inside the group (neighbours from the same pinned group; see the spec); the widget
   (`WidgetContent.build` must put pinned first); strings in `android/strings.tsv` for the five languages, then `python android/scripts/gen_strings.py`). Windows: `Strings.T(en, es, de, fr, pt)` texts, manager row, dock tab marker
   (not the always-on-top pin icon), note menu; the manager's sort and the dock order must call `NoteListing.Sort` (already partitions) and the dock's deck order needs the same partition.
2. **Right-click on the buttons — reviewed (second session): no change.** All four dock footer buttons already answer the right button (Open all: layout and screen; Manager: view and tags; Sync: sync settings; New note: from the clipboard), each tooltip says so (`*RightClickHint`), and each toggles its popup consistently (`ShouldConsumeOpen`). The manager's buttons have no right-click action and do not need one (their actions are on the row and the bulk bar). Android has no equivalent of the dock footer; long-press on a row already opens the note sheet. Original request: (asked at the end of the session): review what the dock buttons do with the right button (`PreviewMouseRightButtonUp` on Open all, Manager, Sync and New note in `EdgeDockWindow.xaml`; handlers `OnOpenAllRightClick`, `OnManageArchiveRightClick`, `OnSyncRightClick`, `OnNewNoteRightClick`),
   whether each is discoverable and consistent, and the same question for the manager's buttons and for Android long-press equivalents.
3. **Android, from the list not done yet:** B10 landscape; C11 bottom sheet hint when there are more options; C13 discoverability of the swipe gestures; C14 task drag handle on the right / left / off (and Windows?); C15 how tags are shown; C16 copy a hyperlink;
   C18 find inside a note on Android; C19 note title next to the back arrow; C20 distribute the options between row menu, sheet, three dots and editor bar (the sheet only lets you add a *new* tag, not an existing one).
   Each one: decide "do / do not" with the reason in this file; anything that touches Windows is reviewed there too.
4. **Checkbox follow-ups:** the real-world "editor stops accepting ticks until restart" was not reproduced (the gap between the box and the text is the likely cause and is fixed); the cursor handle can still cover the next row's box (a system popup).
   An idea left open: a larger line height for task lists to give a taller target.
5. **Before publishing:** `dotnet test Aldune.slnx` (was green: Core 1891, server 29), `./gradlew test` (green) and the device tests; agents `tests` and `seguridad` (sync format and server are touched only by an optional field, but RELEASING asks for it);
   cross-version test of the pin with 2.1.x / 1.0.x (a pinned note travelling and coming back must not break anything; old versions lose the pin when they edit and re-send); `STATUS`, `SETTINGS_PARITY`, `SYNC` (done in draft) and `docs/RELEASING.md`;
   versions in `Aldune.csproj` (2.2.0) and Android (1.1.0, `versionCode`); confirm each publication with the user. Order: Windows first, sync servers after, Android last with `--latest=false`.
6. **Hygiene:** `git log --format='%an <%ae> | %cn <%ce>' origin/main..HEAD` before any push (so far all NachoPola16); the emulator is closed; the Pixel has the *debug* app installed with test notes (your real notes are in the other package, untouched).
7. **Sync review (asked by the owner, second session):** after the pin work, review note sync end to end for bugs on both platforms: pinned notes travelling both ways and through conflicts (`ConflictPayload`, `SyncPlanner`, `SyncMapping`), an edit racing a pin change, deletes and the trash, reordering (`dockPosition`) together with the pin, old 2.1.x / 1.0.x clients re-sending, and the sync servers. Use the `seguridad` and `tests` agents; record findings here before publishing.

## Android list B10 / C11-C20 (second session): do / do not

- **B10 landscape: checked on the Pixel (list + long-press sheet), one bug fixed.** The sheet's actions did not fit in landscape and the last ones (Move to trash, Protect) were unreachable; the sheet now scrolls. Not yet checked in landscape: the editor, the options sheet and Settings. Noted, not changed: with the sync notice showing, the two-pane list has room for only two notes.
- **C11 action sheet hint: DONE.** The long-press sheet opens fully expanded and without the drag handle (nothing more "above" to pull), and it scrolls when the actions do not fit.
- **C13 swipe discoverability: DONE, without a new indicator.** The sheet (long press) now also offers Archive, Move to trash and Restore, the same actions the swipe does. A hint on the row would add noise to every card; the menu everyone already finds is the better place.
- **C14 task drag handle left / right / off: DO NOT, for now.** It is a new setting with its own parity row in `SETTINGS_PARITY` and a layout change in the editor, and nobody reported the handle as wrong where it is. Revisit if it comes back.
- **C15 how tags look: DO NOT.** The "#" is what makes a tag read as a tag in the list; the real complaint (they look like the top tabs) is a styling matter to judge on a device. Left as is.
- **C16 copy a hyperlink: DO NOT in this version.** Needs link detection in the editor plus a long-press target that does not clash with the cursor handles (see checkbox follow-ups). Not worth the risk for 1.1.0.
- **C17 three-dots menu: done earlier.**
- **C18 find inside a note on Android: DO NOT in this version.** A search bar inside the editor interacts with the keyboard, the checkbox tap area and hidden tasks; it deserves its own spec and tests. The menu entry stays.
- **C19 note title next to the back arrow: DONE, as on Windows (reversed after the owner asked again).** The editor has a one-line title field in the header and the body below, like the Windows note window: the note is still ONE text (`NoteText`: first line = title), stored as before, so no sync or format change. Enter in the title moves to the body; a new note focuses the title. A note "title
" keeps its line break when opened and closed (no spurious edit).
- **C20 options distribution: DONE.** Row = quick swipe gestures; long-press sheet = everything about the note in the list (open, move, pin, options, archive, trash/restore, protect); editor three-dots = note actions (options, pin, archive, trash) then, after a divider, text editing (move line, outdent, uncheck all); options sheet = colour, tags (with existing tags as one-tap chips), reminder, pin, export, protect.

## Before publishing: images and GIF (decided: refresh them)

- **Windows: DONE (2026-10-31).** Retaken `aldune.png`, `dock-abanico.gif` and the six `aspecto-*.png`. The GIF's checkbox clicks missed the boxes by ~8 px (also in the 2.1.5 version of the GIF); fixed in the probe, and Milk and Fruit now tick. Original note: `docs/images/aldune.png` (probe8) and `docs/images/dock-abanico.gif` (probe9) show the dock whose footer changed (symmetric 40 px buttons, 17 px icons, drawn plus); retake both, and the six `aspecto-*.png` (probe9) if the footer or the tab badges show in them. Tools: `_herramientasldune-sondas`, close `aldune.exe` first.
- **Android** (`android/fastlane/metadata/android/en-US/images/phoneScreenshots`, taken from the emulator, never from the Pixel with real notes): all four are out of date: `1_notes` (card edges, spacing, pin), `2_checklist` (the title now sits in the header), `3_reminder` (options sheet: existing tags, pin), `4_settings` (tab grid). Retake with English sample notes.
- Social preview and the other README art do not change. The README links to v2.1.5 / 1.0.2 and the installer hash example (`Aldune-Setup-2.1.5.0.exe`) are updated only when the release is published.
- Release text for Android 1.1.0 is in `android/fastlane/metadata/android/*/changelogs/17.txt` (five languages, under 500 characters).

