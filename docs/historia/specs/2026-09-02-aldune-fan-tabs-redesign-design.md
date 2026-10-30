# Aldune — Fan tabs redesign (design spec)

Redesigns how the dock's expanded panel is displayed: from a list of
buttons in an `ItemsControl`/`ScrollViewer` to individual staggered
tabs with a vertical text label, inspired by a reference screenshot of a Hold My Notes-style app that the user shared. See
`docs/STATUS.md`, section "Pending brainstorming idea... fan tabs
Hold My Notes style", for the context of how this came about.

## Context and motivation

The current dock, in its expanded state, shows the notes as a vertical
list of rectangular buttons inside a `ScrollViewer` — functional,
but visually flat. The reference screenshot that motivated this shows
a livelier pattern: at rest, a thin strip with a coloured dash per
note (this **we already have**, unchanged); on mouse hover, the notes
unfold as a fan of tabs that appear staggered in
time, each showing its colour and a text label rotated
vertically; when opening one, the note window seems to grow from the
exact position of its own tab, not appear in a generic place.

It is a real change of interaction and of how `EdgeDockWindow` represents and
animates each note — it does not fit a quick tweak, hence the full spec.

## Scope

**In:**

- The expanded panel (`PanelContent`/`TabsList` in `EdgeDockWindow.xaml`)
  is redesigned: each note is an individual tab with its background colour
  and a vertical text label (the note title, via
  `NoteTitleConverter`, as today). It carries no status label
  ("Archived"/"Trash") because the fan stops showing
  archived/trash — see the controls simplification point, further
  below.
- Layout: aligned column (all tabs protrude the same
  distance from the edge, stacked vertically) — not a cascade in
  depth. Explicitly decided this way as it is the most faithful to the
  reference and the simplest to implement.
- Staggered entrance animation: each tab appears with an increasing
  `BeginTime` (~45ms per index) relative to the previous one,
  combining opacity (0→1) and a translation perpendicular to the edge
  (from "hidden under the pill" to its final position) — a
  slide, not just a fade.
- On clicking a tab, the corresponding `NoteWindow` animates its
  appearance **from the on-screen position of that tab** (captured
  with `UIElement.PointToScreen`) to the final size/position already
  computed by the existing `PositionNoteWindow`/cascade — that cascade
  logic does not change, only the starting point of the
  appearance animation.
- Controls simplification: the "Archived"/"Active" button is removed
  from the dock. The expanded fan shows **only active notes**. The
  gear icon ("Manage notes") remains the only door to
  archived/trash — no changes in `NotesManagerWindow`. The
  "+ New note" and "Manage" buttons go from text buttons in a row
  to two small circular buttons at the end of the tab column.

**Out of scope (does not change):**

- The resting pill (`PillSwatches`, the coloured dashes) — the
  reference already describes it as it is implemented today.
- `EdgeGeometry.PillRect`/`ExpandedRect` and the dynamic size according to the number of
  notes (from the previous session) — the panel keeps using `ExpandedRect`
  for its outer rectangle; what changes is only how it is filled
  inside.
- `NotesManagerWindow`, `AppCoordinator`, `NotesRepository`,
  `NoteWindow` (apart from the new opening animation) — unchanged.
- Multi-monitor/DPI (Phase 3a) — orthogonal to this, already closed.

## Components and changes

### `EdgeDockWindow.xaml` — tab template

The `DataTemplate` of `TabsList` (today a `Button` with `NoteTabButtonStyle`)
becomes a `Border` with:
- `Background="{Binding Color}"` (as today).
- A `TextBlock` with the title (`NoteTitleConverter`) inside a
  `Grid`/`ContentControl` with a `LayoutTransform` of `RotateTransform`
  `Angle="-90"`, so that the text runs from bottom to top along
  the tab (the usual convention for a vertical "folder tab").
- A `MouseLeftButtonUp` (or a chrome-less `Button`, reusing the pattern
  of `NoteTabButtonStyle` but with the new template) to keep
  `OnTabClick` working the same — the logic of which note to open does not change,
  only the visual template.
- `Tag="{Binding}"` is kept, as today, so that `OnTabClick` keeps
  extracting the `Note` the same way.

Since the fan no longer shows archived/trash, the status label `TextBlock`
(`NoteStateLabelConverter`) that today lives in the same
`DataTemplate` is **removed** from here, and with it the declaration of
`<local:NoteStateLabelConverter x:Key="NoteStateLabelConverter" />` in
`EdgeDockWindow.xaml.Resources` (it is left unused in this file). The
converter itself is not deleted — it still exists and is still used in
`NotesManagerWindow.xaml`, which does not change.

### `EdgeDockWindow.xaml` — controls at the end of the column

`NewNoteButton`/`ToggleArchiveButton`/`ManageArchiveButton` (today a row
of text/icon buttons) are replaced by two small circular buttons
(same size as each other, ~28-32px in diameter) stacked at the end
of the tab column, inside the same `ScrollViewer`/container:
"+" (create note, same `OnNewNoteClick`) and the gear icon (same
`OnManageArchiveClick`, now without needing `_coordinator.OpenOrActivateNotesManager()`
to be conditioned on anything). `_viewingArchive`, `OnToggleArchiveClick`, and the
references to `NewNoteButton.Visibility`/`ToggleArchiveButton.Content` are
removed from `EdgeDockWindow.xaml.cs` — the fan no longer has two views.

### `EdgeDockWindow.xaml.cs` — staggered entrance animation

When `TabsList.ItemsSource` changes (inside `SetNotes`, which is already
called on every `Refresh()`), after the `ItemsSource` update, we
have to wait for the `ItemsControl` to generate the containers
(`ItemContainerGenerator.StatusChanged` or `Dispatcher.BeginInvoke` with
`Loaded` priority, the standard WPF pattern for "do it after the
items' layout exists") and, for each generated container in order,
launch an opacity + `TranslateTransform` animation with
`BeginTime = TimeSpan.FromMilliseconds(45 * index)`. Before launching
new animations, clear (`BeginAnimation(dp, null)`) any that
might exist from a previous generation of items — the same defensive pattern that already
exists for `PanelContent`/`PillSwatches` in `ApplyGeometry`, so that a
quick hover in/out (which fires `SetNotes`/`Refresh` several
times in a row) does not fire orphaned animations or accumulate handlers.

**Important (lesson from Phase 3a)**: these animations must always carry
an explicit `From` (opacity 0, translation at the "hidden" value)
— never rely on WPF looking at the "current" value of the property as an
implicit origin, for the same reason that caused the
`AnimationException: ... default origin value of 'NaN'` in
`EdgeDockWindow.ApplyGeometry`.

### `EdgeDockWindow.xaml.cs` — opening animation from the tab

`OnTabClick` (or the equivalent new handler on the `Border`) captures the
on-screen position of the clicked element before asking the coordinator
to open the note:

```csharp
var tabElement = (FrameworkElement)sender;
var screenPosition = tabElement.PointToScreen(new Point(0, 0));
var tabRect = new Rect(screenPosition.X, screenPosition.Y, tabElement.ActualWidth, tabElement.ActualHeight);
_coordinator.OpenOrActivateNote(note, this, tabRect);
```

`AppCoordinator.OpenOrActivateNote` gains a `Rect originRect` parameter
(in screen pixels/DIPs of this monitor — the same coordinate space
as `Window.Left/Top`, since `PointToScreen` on an element of a window returns coordinates already in the same system that
that window's `Window.Left/Top` uses), which it passes to `NoteWindow` so that it
animates its appearance from there. If `originRect` is null (not applicable, e.g.
if in the future a note is opened from somewhere other than a click on
a tab), `NoteWindow` uses its current behaviour (appearing
directly at the final position, with no growth animation).

`NoteWindow` needs, after `PositionNoteWindow` has set its
final position/size (`Left`/`Top`/`Width`/`Height` already computed by the
existing cascade), to animate **from** `originRect` to those final
values, with explicit `From`/`To` (same pattern as
`EdgeDockWindow.ApplyGeometry`) — probably in an
`AnimateFromOrigin(Rect origin)` method called right before `Show()`.

### `AppCoordinator.cs`

`OpenOrActivateNote(Note note, EdgeDockWindow requestingDock)` gains a
third parameter `Rect? originRect = null` (optional, with a default value so as not to break other calls if there
are any) and forwards it when building the `NoteWindow`.

## Error handling

- If the `ItemsControl` has not yet generated containers when
  the staggered animation is attempted (e.g. collapsing and re-expanding very quickly, before the
  previous generation has finished), the
  code must check `ItemContainerGenerator.Status` and not assume that
  the container already exists — if it does not exist, that tab is skipped
  with no entrance animation instead of throwing a null-reference exception.
- No new startup-failure or repository failure cases — this is
  purely presentation.

## Testing

There is no new `Aldune.Core` logic to test — the position of each
tab is standard WPF layout, and the on-screen position used to animate
the opening is read with `PointToScreen` at the moment of the click, with no
own calculation to isolate into a pure function. It is verified by hand,
consistent with the rest of this app's window mechanics.
Suggested manual checklist:

1. On hover, the tabs appear staggered in time
   (not all at once), each sliding in from the edge with its
   own colour and the vertical text label legible.
2. Click on a tab → the note window seems to grow from the
   position of that tab, not appear in a generic place.
3. With several notes already open, the positioning cascade still
   keeps them from overlapping exactly (existing behaviour, must
   not break).
4. With many notes (more than fit in `ExpandedMaxLength`), there is still
   scrolling inside the panel.
5. Collapsing and re-expanding quickly several times in a row does not crash
   or fire accumulated/erratic animations.
6. The "+" button and the gear one work as before (create note,
   open "Manage notes").
7. The "Archived" button no longer exists in the dock; going into "Manage
   notes" and filtering by Archived/Trash still works as before.
