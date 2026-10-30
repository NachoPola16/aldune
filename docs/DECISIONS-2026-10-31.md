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
