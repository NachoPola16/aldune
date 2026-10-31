# Reviewing the translations

Aldune ships in eight languages: English, Spanish, German, French, Brazilian Portuguese, Italian, Japanese and Simplified Chinese (the
Windows installer has no Chinese). English and Spanish are written by the author. German, French and Brazilian Portuguese were produced
with assistance and read by the author. **Italian, Japanese and Chinese are machine-assisted and have never been read by a native speaker**:
they are understandable, but a person who speaks the language will find stiff or wrong wording. That is what this page is for.

## How a reviewer can help (no coding needed)

1. Install the app in your language (Windows: Settings → General → Language; Android: Settings → General → Language) and use it for a
   few minutes. Anything that reads oddly, is cut off or is not translated is worth reporting.
2. Open an issue with the **Translation review** template: language, where you saw it, what it says and what it should say. A list or a
   screenshot is enough.
3. Prefer small, concrete fixes («“Trash” should be X, not Y») over a rewrite. The author merges them; you stay credited in the commit
   message if you want.

## Where the texts live (for someone who wants to send a pull request)

| App | File | Format |
|---|---|---|
| Android | `android/strings.tsv` | One row per string, one column per language (`en es de fr pt-BR it ja zh`). Edit the cell, then run `python android/scripts/gen_strings.py` to regenerate the XML (`--check` verifies it). |
| Windows | `src/Aldune/Resources/Strings.cs` | Each string is `T(en, es, de, fr, pt, it, ja, zh)` in that order. |

Keep placeholders (`%1$s`, `{0}`) and keep texts short: buttons and dock labels have little room.

## Terms worth agreeing on first

The same few words appear everywhere and decide whether the whole app feels consistent. For each language a reviewer can answer these
once and the rest follows:

- **Note, Archive, Trash, Restore, Pin, Tag**
- **Dock** (the tab strip at the screen edge), **Fan** (the open strip), **Manager** (the notes window)
- **Sync**, **Profile code**, **End-to-end encrypted**
- **Task / checkbox**, **Reminder**, **Look** (the colour and shape theme)

## Checking a language yourself (author or reviewer)

- Android: `python android/scripts/gen_strings.py --check` fails (and writes nothing) if a language is missing a string or a row has the wrong number of columns.
- Windows: nothing checks it automatically; a missing language shows in English, so look for English text in the other languages.
- Look at the longest strings in German and Japanese in the real UI at 150 % text size: that is where clipping shows first.

## Status

| Language | Status |
|---|---|
| English, Spanish | Author |
| German, French, Portuguese (Brazil) | Assisted, read by the author |
| Italian, Japanese, Chinese (Simplified) | Machine-assisted, **waiting for a native reviewer** |
