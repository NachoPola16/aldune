# Note themes — design

Date: 2026-09-23. Part of Phase 2 of the 1.0 closing plan
(plan withdrawn from the tree). Reference mockup: palettes A and B and
the "top edge" finish, validated by the user on the same day.

## Goal

Let the user decide the look of their notes without having to pick the colour of each one:

- Choose a **theme**: a set of dark and light colours designed to go together.
- Choose whether new notes are born **light, dark, or of both kinds**.
- Choose **how the colour is assigned** to each new note.
- **Create their own themes**.
- **Apply a theme to the notes that already exist**.

Conditions: the custom colour of each note is kept; the current theme remains the factory one,
so updating the app changes nothing until the user decides to.

## Out of scope (1.0)

- **Theming the interface** (dock, menus, settings, manager). There are 361 hand-written colours in the
  XAML and they would have to be moved to tokens first. Left for 1.x.
- **Syncing themes and colour settings.** Note colours already travel with each note, so
  notes look the same on all devices. What does not travel is the configuration, and it also
  makes sense for it to be per device. Syncing it would open up editing conflicts in exchange for
  little. Left for 1.x if it is missed.

## Model

### Theme

```text
NoteTheme
  Id           string   stable; "classic", "serene", "graphite" for the built-in ones; GUID for custom ones
  Name         string
  DarkColors   string[] #RRGGBB, in rotation order
  LightColors  string[] #RRGGBB, in rotation order
  IsBuiltIn    bool     built-in ones are neither edited nor deleted; they can be duplicated
```

It lives in `Aldune.Core`, with no WPF dependencies. The built-in themes are constants in code; custom
ones are stored in `AppSettings.CustomThemes`.

Built-in themes, with the values already computed in OKLCH (same L within each group):

| Theme | Dark | Light |
|---|---|---|
| **Classic** (`classic`) | — | The current 6 from `NoteColorPalette.Colors` (L 0.87) |
| **Serene** (`serene`) | Graphite `#2E3034`, Slate `#26323E`, Ink `#262F47`, Petrol `#1D3538`, Moss `#283426`, Tobacco `#3C2D21`, Burgundy `#462527`, Plum `#392A3C` (L 0.31) | Bone `#EBE6D9`, Stone `#EBE5E0`, Mist `#DEE8F0`, Sage `#DEEADE`, Linen `#F0E4D7`, Dust `#F2E2E1` (L 0.925) |
| **Graphite** (`graphite`) | Charcoal `#2F2D2C`, Graphite `#2B2E33`, Smoke `#332C29`, Steel `#262F36`, Onyx `#2E2E2E` (L 0.30) | Paper `#EBE7E0`, Chalk `#E4E8ED`, Sand `#EFE6DD`, Ash `#E8E8E8` (L 0.93) |

A theme can have only one of the two lists (Classic only has light ones). If the requested tone does not
exist in the theme, the other list is used: a note is never left without a colour.

### New settings (`AppSettings`)

| Field | Type | Default | Note |
|---|---|---|---|
| `ActiveThemeId` | string? | null = `classic` | If it points to a deleted theme, it falls back to `classic`. |
| `NewNoteTone` | enum `Light`/`Dark`/`Both` | `Light` | With `Both`, dark/light alternate. |
| `ColorAssignment` | enum `Rotate`/`MostDistinct`/`RotateAvoidNeighbors`/`Fixed` | `RotateAvoidNeighbors` | |
| `FixedNoteColor` | string? | null = first colour of the requested tone | Only with `Fixed`. |
| `CustomThemes` | List\<NoteTheme\> | empty | |

All of them have a default value that works when the field is missing: a previous `settings.json`
is read without migration (same criterion as `GlobalHotkeyEnabled`).

## Colour assignment (`NoteColorAssigner`, Core, pure and tested)

Input: the theme, the requested tone, the rule and the colours of the active notes **in dock
order**. Output: the `#RRGGBB` of the new note.

1. **Candidates**: the list of the requested tone. With `Both`, dark if the last note in the dock is light
   and light if it is dark (dark if there are no notes).
2. **Rule**:
   - `Rotate`: the candidate following the last colour of that tone used in the dock. If there is
     none, the first. It replaces the current `existing % Colors.Length`, which repeated colours when
     deleting notes.
   - `MostDistinct`: the candidate whose minimum OKLab distance to the dock's colours is greatest.
     In a tie, the first in list order.
   - `RotateAvoidNeighbors`: like `Rotate`, skipping candidates equal to the colours of the
     **two notes between which the new one will appear** in the dock. If all candidates are
     excluded (a theme with one or two colours), it falls back to `Rotate`.
   - `Fixed`: `FixedNoteColor`, or the first candidate if there is none.

Colour comparison is case-insensitive on the hex.

It replaces the two copies of the current assignment (`AppCoordinator.CreateAndOpenNote` and
`EdgeDockWindow.CreateNote`), which now call a single point.

## Derived colour: border and label (`NoteColorDerivation`, Core)

Today the border (`Rims`) and the label (`Labels`) are hand-written for the 6 factory colours,
and the rest is darkened with an approximate RGB factor. With themes, any colour has to come out
right, so they are computed in OKLCH from the face:

- **Light face** (L ≥ 0.6): border at L−0.16 and label at L−0.44, same hue and chroma. It is the rule
  with which the current values were designed. The 6 factory ones **keep their exact hex**
  through a table of exceptions (they were adjusted by hand and the formula does not reproduce them pixel for pixel; a
  test pins it). The computed ones are fitted to the sRGB gamut by reducing chroma.
- **Dark face** (L < 0.6): border at L+0.07 (the "edge") and label at L 0.86, with chroma
  `min(C·1.6, 0.08)`.
- If the computed label does not reach 4.5:1 against the face, `ForegroundFor(face)` is used, as
  today.

`NoteColorPalette.RimFor/LabelFor` delegate to this class; `Darken` goes away.

## Tab finish

- **Light face**: unchanged (`TabSheen` highlight and 1 px border with `RimFor`).
- **Dark face**: no highlight. Instead of the full border, a 1 px line at the top with the colour of the
  edge (`RimFor`), and the same shadow. It is decided per tab according to the luminosity of its face,
  in the same place where `TabSheen`/`TabSheenLeftEdge` is chosen today.
- The open note window already uses `RimFor` and `ForegroundFor`; with the new derivation, the
  dark ones come out with a coherent header and ink without further changes.

## Interface

### Settings → "Themes" section (new, before "Sync")

- **Theme**: dropdown with the built-in and custom themes. Below it, a strip with its colours.
- **Buttons**: "Duplicate", "New", "Edit" and "Delete". Edit and delete are only
  available for custom themes.
- **New notes are born**: Light / Dark / Of both kinds.
- **Colour of new notes**: Rotate through the theme / The most distinct / Rotate without repeating the one
  next to it (recommended) / Always the same colour, with a picker for that colour.
- **"Apply to existing notes…"** opens a confirmation: *"The colour of your N
  active notes will be changed, including the colours you picked by hand. It will be synced to your other
  devices."* It goes through the active notes in dock order and assigns each one with the current rule
  and tone, as if they were created in that order. Archived and trashed ones are not
  touched.

### Theme editor (small window)

Name and two rows, "Dark" and "Light". A colour is clicked to select it, and the buttons "←",
"→" and "Remove" act on the selected one. They are added with "+", which opens the existing
`CustomColorWindow` (it only lets you choose legible colours). With buttons and not dragging, which is more reliable and
works with the keyboard. With a single empty list the theme is valid; with both empty, it cannot be
saved.

### Quick access

The quick palette of the note's "⋯" menu and that of the dock tab menu show the colours
of the active theme: dark ones first and then light ones, in two rows if there are both kinds. The
"Custom colour…" stays the same.

### Texts

All through `Strings.T(en, es)`, like the rest of the app.

## Data and sync

- The database schema and the sync format do not change: the note still stores a
  `#RRGGBB`.
- "Apply to existing notes" is a normal colour edit of each note, so it is
  synced just as if they were changed by hand.
- The themes and the new settings live in `settings.json`, local to each device.

## Errors and edge cases

- Active theme deleted, or `settings.json` with an unknown id: `classic` is used.
- Custom theme with invalid colours (malformed hex, edited by hand in the JSON): those colours are
  ignored on load; if none remains, the theme is discarded.
- `FixedNoteColor` invalid or not from the active theme (chosen in another theme): first candidate.
- Dock without notes: `Rotate` and `RotateAvoidNeighbors` give the first candidate; `MostDistinct`,
  too.

## Tests

- `NoteColorAssignerTests`: each rule, `Both` alternating, fallback to the other list, single-colour theme,
  empty dock, uppercase or lowercase hex, and that deleting notes no longer repeats neighbours.
- `NoteColorDerivationTests`: the 6 factory ones give exactly their current `Rims`/`Labels`; every
  colour of the built-in themes gives a label ≥ 4.5:1 against its face; the computed colours are in
  gamut.
- `NoteThemeTests`: valid built-in themes; loading ignores invalid colours; falls back to `classic`.
- `SettingsServiceTests`: a `settings.json` without the new fields loads with the default
  values.
- Visual verification with real rendering (`RenderTargetBitmap`) of light and dark tabs, and
  manual check by the user in the dock.
