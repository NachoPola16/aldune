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
