# Design

Source of truth in code: `src/Aldune.Core/AppPalette.cs` and `AspectPalettes.cs` (chrome by semantic keys), `NoteThemes.cs` (note colors). This document summarizes the system; if they disagree, the code wins.

## Visual theme

Scene: someone with another app open, under office light or at night at home, looking at Aldune for a few seconds. That is why the default is a warm dark (toasted brown, not black or blue-gray) and a warm paper light (not pure white). Color strategy: **Restrained** in the base app (warm neutrals and a sand accent), **Committed** in each retro look, where the whole skin is the look.

## Colors

Chrome by semantic keys (`Ground`, `Surface`, `Raised`, `Text`, `MutedText`, `Accent`, `Danger*`, `Border`, `TitleBar`…), never hex in XAML.

| Role | Dark | Light |
|---|---|---|
| Background (`Ground`) | `#2A261F` | `#F6F1E8` |
| Text | `#EDE7DC` | `#2A261F` |
| Accent | `#A18465` | equivalent sand/toast |
| Danger (text) | `#E8A0A0` | derived |

Looks: Dark, Light, Pastel (lavender with a pink accent), Midnight (night blue), XP light/dark (Luna, blue gradient bar, Tahoma), Telecom light/dark (lab paper / oscilloscope, channels CH1-CH4), Bash (Gruvbox), Phosphor (monochrome), Travel notebook, Field notebook and Graph-paper notebook (light and dark). The “system theme” follows Windows.

Note colors: by themes (Classic, Serene, Graphite, Gray, Pastel, Autumn, Ocean, XP) with light, dark or alternating tone; border and label are derived from the face color (`NoteColorDerivation`). The logo uses yellow `#EBD38B`, cyan `#83E7F2` and pink `#F9BEF2` on `#2A261F`.

## Typography

Segoe UI (Segoe Fluent Icons for glyphs) in the base app; Tahoma in XP; monospace in Telecom, Bash and Phosphor; handwritten or paper fonts in the notebooks. The note body defines its own font and line height (`AlduneNoteBodyFont`, `AlduneNoteLineHeight`) and everything that lines up with the text (empty-note marker, rules) uses them. Text size from 100 to 150 %.

## Elevation and shape

Flat by default, with a thin border and rounded corners; “Square corners” removes them. The retro looks use bevels (`BevelLight`/`BevelDark`) instead of a diffuse shadow. No glassmorphism. An icon inside a button with a bevel or a one-sided shadow can look off-center even when it is centered: the geometry is measured, not the impression.

## Components

A tab dock on an edge (it fans out), notes as windows with a header and tags, the manager, Settings by tabs, toasts (`ToastWindow`), dialogs (`AppDialog`), a color picker. On Android: list, editor, Settings by tabs, options sheet, widgets. Visible focus on all of them.

## Motion

Short and functional (dock fan-out, note opening). Soft ease-out curves, no bounce. “Reduce motion” is respected.

## Do / Don't

- Do: `AppPalette` keys, text through `Strings.T`, measured centering, contrast fixed by tests.
- Don't: loose hex values, shadows without thinking about optical centering, retro that lowers contrast, features on one platform only without a reason.
