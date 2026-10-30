# Decisions, 2026-10-31

Work list: the pending changes for Windows 2.2.0 and Android 1.1.0 (one release of each, nothing published in between). Each item is "do" or "do not" with the reason.

## A. Bugs

### A1. Trash (Android) — DONE
- **Swipe stops working after restore + Undo.** Reproduced on a Pixel 10a with a Compose instrumented test (`TrashSwipeTest`): the row's `SwipeToDismissBoxState` came from `rememberSwipeToDismissBoxState`, which uses `rememberSaveable`, so the state was restored already dismissed. It is now built with `remember(item.id, item.state)`, always at rest. Windows has no equivalent (the manager uses buttons, not swipes).
- **Menu / "Open note" on a note that is gone.** The long-press menu and the delete confirmation kept a stale `NoteListItem`. They now close when their note leaves the list and always show the live item. Not reproduced by hand (it needs a finger); fixed defensively because the stale reference is evident in the code.
- Known and left alone: `confirmValueChange` calls `onDelete` several times per gesture (it returns `false`, so the state retries); it only sets `deleting`, so it is idempotent.

### A2. Task checkboxes (Android) — PARTLY DONE
- **Wrong row ticked.** Reproduced on the Pixel with a 60-task note scrolled to the end: the counter went up but none of the visible rows was ticked. Cause: the tap position was resolved against the text layout without adding the field's scroll (the task drag does add it). Fixed in `NoteEditorPane.kt` (viewport → layout coordinates at tap time, so a scroll during the one-frame delay does not matter). Eight rapid taps now tick exactly the eight rows tapped.
- **Editor stops accepting ticks until the app restarts.** Not reproduced (synthetic `adb` taps, fast or slow). It is likely a consequence of the same mis-resolution (a tap decided as "not on a checkbox" lets the field take the cursor), but that is a hypothesis; watch it in real use.
- Windows: not affected (a WPF text box; `TaskLines` in Core is only text logic and was not involved).
- No automated regression test: the editor is too coupled to the view model to host in a Compose test. The probe is documented in `ANDROID_PROBES.md`.
