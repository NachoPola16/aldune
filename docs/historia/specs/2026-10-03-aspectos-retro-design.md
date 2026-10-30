# Aldune — Retro looks, shape options and single colour (design spec)

Decided with the user on 2026-10-03, based on mockups (seven artboards,
each with its own *Tweaks*).
The mockups are the visual reference; this spec fixes what gets built and how it fits into the code.

## What was asked for

- "More retro" looks: XP, Windows 95, Linux terminal, telecommunications, phosphor monitor.
- That the colours of those looks can be **customised** (the colour of `nacho@aldune`, the XP bar colour,
  etc.).
- **Square** corners on notes, dock and windows, and a **sync signal**, selectable in **any** look.
- Being able to paint **all notes the same colour** (monochrome) without changing them one by one, with any
  theme.
- A better dark note theme than the current ones, and a review of the existing themes.
- Export and import of the visual configuration and the settings (settings sync does not exist).
- The dock redesigned from the real one (resting strip + 208×52 cards + footer), not invented.

## Out of scope

- Markdown, notes linked to files, and photos (their own spec, later).
- MSIX (spec of 2026-09-28, after this one).
- Changing the structure of the dock or the note: looks change how the same pieces are painted.

## Decisions

### 1. Look = palette + skin

Today a look is just a palette of ~55 colours (`AppPalette`). A retro look also needs **shape**. The two
are separated:

- **Palette** (chrome colours, in Core): as today. The retro looks **derive** it from Dark or Light
  with the same mechanism as Pastel and Midnight (`AppPalette.Tint`: the OKLCH lightness of each key is
  kept and the hue is changed), plus the colours chosen by the user. This way they inherit the contrast
  that is already verified. New keys: `TitleBar`, `TitleBarEnd` (gradient), `OnTitleBar`, `BevelLight`,
  `BevelDark` (Windows 95 relief). In Dark and Light they have the same value as what is seen today (bar
  = background, no relief), so the current looks do not change.
- **Skin** (`AppSkin`, in Core, pure data): typography of the chrome and the notes, border style
  (`Flat` | `Bevel`), title bar (`Plain` | `Gradient`), dock card style
  (`Filled` | `Stripe` | `Tinted`), text decorations (none | channel prefix `CH1` | folder suffix
  `hoy/` | uppercase) and prompt line in the note (yes/no). The WPF layer turns it into
  resources (`AldunePrimaryFont`, `AlduneNoteFont`, styles with `DataTrigger` on the border style)
  just as `ThemeManager` does with the palette.

`AppearanceMode` gains new values (5 onwards, written as a number; an old `settings.json`
still loads, and an earlier version that reads an unknown value falls back to Dark, which is the
`_` of `AppPalette.For`).

### 2. The new looks

| Look | Base | Skin | Colours the user chooses |
|---|---|---|---|
| **XP light** (Luna) | Light | Tahoma; vertical-gradient bar; buttons with gloss; `Filled` cards; rounded corners by default | Bar (Luna blue, olive, silver, tile red…), accent (new-note button, checkboxes) |
| **XP + 95 dark** | Dark | Tahoma; Windows 95 relief (`Bevel`); Windows 98 horizontal-gradient bar; square corners | Bar (navy blue, teal, purple, maroon…), accent |
| **Telecom light** (lab) | Light (paper) | Cascadia Mono; 1 px lines; `Tinted` cards per channel with their waveform; `CH1…` prefix; square | Ink, accent |
| **Telecom dark** (oscilloscope) | Dark (panel) | Same, with a background grid on the note | Panel, trace (CH1) |
| **Bash** | Dark | Cascadia Mono; notes with an **untinted** terminal background and the colour only in a stripe (`Stripe`); `hoy/` suffix; prompt line `usuario@aldune:~/notas$`; square | Palette (Gruvbox by default, Ubuntu, Tango), and on top of it user, path and accent |
| **Phosphor** (old monitor) | Dark | Cascadia Mono; uppercase; no glow or sweep; square | Phosphor (green, amber, blue, white), details |

Details that came out of the review with the user and must be respected:

- XP + 95 dark: the note is neutral graphite, **without** the brown/orange of Royale Noir.
- Bash: no strong colours by default (muted Gruvbox); notes **always dark**, only the
  stripe carries colour; card title in the text colour, not in the note's colour.
- Phosphor and telecom are different looks: the channels (`CH1`, `RX`, waveforms) only
  in telecom. Oscilloscope channel colours: CH1 yellow, CH2 cyan, CH3 magenta, CH4
  blue (darkened in the light variant); the channel and its waveform (sine, square, triangle,
  sawtooth) depend on the **position** in the dock, cycling every 4.
- The decorations are presentation only: the note text does not change (`hoy/` or `CH1 HOY` are only painted).

**Customisable colours**: each look declares its slots (`AspectColorSlot`: id, visible name,
default value, role in the derivation). They are stored in `settings.json` as
`AspectColors: { "<aspecto>": { "<hueco>": "#RRGGBB" } }` (missing = default). Settings → Appearance
shows the slots of the chosen look, each with its swatch, its value and "Change" (the colour picker
that already exists, `CustomColorWindow`), plus "Reset colours". So that a chosen colour does not
break legibility, the derivation **adjusts the lightness** of the user's colour when needed (for
example, a bar too light for white text is darkened until 4.5:1): the hue is kept,
which is what the user is choosing. Tests with extreme colours (white, black, pure yellow).

### 3. Options for any look

In Settings → Appearance, below the colours, they apply to all looks (including today's):

- **Square corners on notes and dock** (`SquareCorners`, `bool?`; `null` = whatever the look says: square
  in bash, telecom, phosphor and XP + 95; rounded in the rest). It requires moving the hand-written radii
  (~83 `CornerRadius` in 12 XAML files) to resources `AlduneRadiusSmall/Medium/Large/Pill` and
  `AlduneTabRadius` (the one for the tab attached to the edge, which is mirrored with the dock on the left: today
  `ApplyLeftEdgeTabShape` does it in code). With square corners, `NativeMethods.ApplyRoundedCorners` asks
  DWM for square corners (`DWMWCP_DONOTROUND`) instead of the small ones.
- **Sync signal** (`ShowSyncSignal`, default depends on the look): at the foot of each note,
  `▂▄▆█ sync` in monospace. Real state, computed in Core (`NoteSyncSignal`): synced (its date
  matches the stored base version), pending upload, in conflict, or not synced (excluded
  by selective sync). With no sync configured it is not shown.
- **Same colour on all notes** (`UniformNoteColor`: `null` = disabled, or `#RRGGBB`): it only
  changes how they are **painted** (note, dock tab, strip capsule); each note keeps its stored colour,
  so disabling it brings everything back and sync sees no changes. The colour is chosen
  among those of the active note theme or freely; ink, border and label are derived from it
  as always (`NoteColorDerivation`). In the `Stripe` style, the single colour is that of all stripes.
  A single place decides the visible colour (`NoteDisplayColor.Resolve(color, settings)` in Core) and
  the dock converters and `NoteWindow.ApplyColor` use it.

### 4. Note themes

Measured review (minimum OKLab distance between two colours of a theme; the tests require ≥ 0.03):
Classic 0.074, Pastel 0.044, Autumn 0.033, Ocean 0.030, **Serene 0.020 (dark) and 0.008 (light)**,
**Graphite 0.004–0.006**. Decided:

- **Serene is renewed** (same id `serene` and same name). Colours with the same lightness,
  just enough chroma to separate, and hues spread evenly:
  - Dark (L 0.31, C 0.052, every 45°): `#472525 #422A11 #36310E #1F371F #033936 #113447 #2B2D4A
    #3E273F` (minimum distance 0.033, ink contrast ≥ 10:1).
  - Light (L 0.925, C 0.042, every 60°): `#FFDDD4 #EFE7C7 #D3EFD8 #C7EFF4 #D9E7FF #F5DDF6` (0.037,
    ≥ 13:1).
  It replaces the "Penumbra" of the mockups: two almost identical muted themes were redundant. The
  existing distance and contrast tests are extended to all built-in themes (today they do not cover Serene
  or Graphite, which is why they passed).
- **Graphite is removed**: its role (monochrome) is better covered by "same colour on all notes", which lets
  you choose the grey. Migration: whoever has it active moves to `UniformNoteColor` with its grey (`#2E2E2E` or
  `#E8E8E8` depending on `NewNoteTone`) and to Classic as theme; their notes do not change (they store their colour). A
  `settings.json` that names `graphite` already falls back to Classic (`NoteThemes.Resolve`).
- **Serene recolouring**: once, on starting the new version, each note whose colour is one of the old
  Serene colours moves to the new one of the same position and tone (dark → dark, light → light). It is a
  normal colour change (`NotesRepository.SetColor`) and it is synced. It is flagged in `settings.json`
  (`SereneRecolored`) so it is not repeated. Risk: two machines that update at the same time recolour the same
  note with the same result; sync has to treat it as the same change and not as a
  conflict. Sync integration test before considering the migration good; if today it flagged it as
  a conflict, it is fixed there (identical content and colour = no conflict).
- Classic, Pastel, Autumn and Ocean do not change. The Pastel and Midnight looks do not either.
- **XP** (light): the post-it colours of the mockups (`#FFFFE1 #E1F0FF #E8F5D8 #FCE4D6 #EDE3F7 #FFF0C2
  #DDEFEF #F0EFEA`), same tests.
- Each look **proposes** a theme when chosen, with the question
  "Use the X note theme as well?": the user's theme is not changed without asking. Look and theme
  remain independent. (XP light → XP; the dark ones → Serene.)
- With `Stripe` (bash), the stripe needs to be visible against the dark background even if the note colour is
  muted: it is derived from the note colour by raising its lightness (L≈0.72) and with a minimum chroma, in Core
  (`NoteColorDerivation.StripeColor`), with a contrast test ≥ 3:1 against the background.

### 5. The dock in each look

Same structure and measurements as today (`EdgeDockWindow.xaml`: 20 px resting strip with 12×26
capsules; 208×52 cards attached to the edge with title and preview; pill footer with See all,
Manage, Sync and a 40 px New note). What changes per skin:

- Cards: `Filled` (today), `Stripe` (chrome background + 3 px stripe on the left, or on the right
  with the dock on that side), `Tinted` (soft tint + stripe + waveform of its channel). In `Tinted` the
  colour is that of the **channel** (by position in the dock), not of the note: that is what gives `CH1…` its meaning.
  With "same colour on all notes" enabled, the single colour replaces the channel ones and only the
  waveforms and the channel number keep distinguishing the cards.
- `Bevel` borders: light relief at top-left and dark at bottom-right on cards, capsules, footer and
  buttons.
- Footer: with square corners it stops being a pill; the round buttons become square.
- Telecom: the waveform is a fixed `Path` per channel (four geometries in resources).

### 6. Exporting and importing the configuration

One file, `*.aldune-config.json`, from Settings → About ("Export configuration…",
"Import configuration…"):

- On export the sections are ticked: **Appearance** (look, custom colours of each look,
  corners, sync signal, same colour, active note theme, custom themes, tone and colour assignment
  of new notes) and **Settings** (language, interface mode, shortcuts, dock: edge, view, keep
  open, preview, full screen, trackpad; tasks: move done, hide done and its deadline;
  trash; remembered positions yes/no; version check).
- They are **never** exported: sync profiles and passwords, keys, the chosen screen
  (`MonitorInfo.StableId` is specific to each machine), window positions, start with Windows (it belongs to
  each machine's registry).
- Format: `{"format": "aldune-config", "version": 1, "app": "1.5.0", "appearance": {…}, "settings": {…}}`.
  A missing section is left untouched on import. Unknown fields are ignored; invalid colours are
  discarded (`NoteThemes.Sanitize` for custom themes); out-of-range enums fall back to their default
  value. A file from a future version (`version` > 1) is still read with whatever is understood.
- On import: a preview with the list of what would change (current value → new value), buttons
  "Apply" and "Cancel". It is applied live (look and shortcuts without restarting). Before applying, a copy of the
  current `settings.json` is saved next to it (as the daily copy already does), so it can be
  rolled back.
- Logic in Core (`ConfigExport.Build(settings, sections)` and `ConfigImport.Plan(json, current)` →
  list of changes + resulting settings), with tests: round trip, partial sections, corrupt
  file, odd fields, and that credentials and the screen never come out.

## Order of work

Three blocks that can be released separately, each with its tests and its probe:

1. **Cross-cutting options and themes**: radii to resources, square corners, sync signal, same colour,
   renewed Serene (with its recolouring), Graphite out (with its migration) and XP. It does not change any existing
   look except through the new options.
2. **Skin**: `AppSkin`, typography, relief, gradient bar, card styles, decorations, prompt; the
   new palette keys with their neutral values in Dark/Light.
3. **The six looks** with their colour slots and the expanded Settings → Appearance page.
4. **Export and import** of the configuration (independent of the others; can go at any point).

All before 1.5.0, together with the lists and the shortcut already done.

## Tests

- Core (TDD): derivation of each retro palette with the default colours and with extreme colours
  (contrast of all texts on their backgrounds, like the current palette tests);
  `NoteSyncSignal` in its four states plus "no sync"; `NoteDisplayColor.Resolve` (disabled, single
  colour, invalid colour in `settings.json`); `StripeColor` (contrast ≥ 3:1 with each dark background);
  distance and contrast in **all** built-in themes (renewed Serene and XP included); Serene recolouring
  (each old colour to the new one of its position, only once, foreign colours untouched); Graphite
  migration; sync with the same recolouring on two machines without conflict; export/import; loading an old
  `settings.json` without the new fields.
- Probes with temporary data: screenshots of note, dock (resting and expanded, on the right and on the
  left) and Settings in each look, with square and rounded corners; live look change;
  same colour enabled and disabled. They are compared with the mockups.
- New texts in the five languages with `Strings.T` (look names, colour slots, options).
