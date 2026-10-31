#!/usr/bin/env python3
"""Genera los `strings.xml` de la app desde `android/strings.tsv` (clave + los ocho idiomas).

Falla (y no escribe nada) si falta una traducción, como la regla de `Strings.T` en Windows. Se ejecuta a mano tras editar la tabla:

    python android/scripts/gen_strings.py          # genera
    python android/scripts/gen_strings.py --check  # solo comprueba que la tabla y los XML coinciden (para CI)

Columnas: key, en, es, de, fr, pt-BR, it, ja, zh. `en` va en `values/` (el idioma de reserva); los demás en `values-es`, `values-de`, `values-fr`,
`values-pt-rBR`, `values-it`, `values-ja` y `values-zh-rCN`.
"""
import re
import sys
from pathlib import Path
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parent.parent
TABLE = ROOT / "strings.tsv"
RES = ROOT / "app" / "src" / "main" / "res"
FOLDERS = {"en": "values", "es": "values-es", "de": "values-de", "fr": "values-fr", "pt-BR": "values-pt-rBR", "it": "values-it", "ja": "values-ja", "zh": "values-zh-rCN"}


def android_text(value: str) -> str:
    # Escapes de Android: el apóstrofo y la arroba al principio rompen el recurso; `%1$d` se deja tal cual.
    text = escape(value).replace("'", "\\'").replace('"', '\\"')
    return "\\@" + text[1:] if text.startswith("@") else text


def load():
    lines = TABLE.read_text(encoding="utf-8").splitlines()
    header = lines[0].split("\t")
    if header != ["key", *FOLDERS]:
        sys.exit(f"Cabecera inesperada en {TABLE.name}: {header}")
    rows, errors, seen = [], [], set()
    for number, line in enumerate(lines[1:], start=2):
        if not line.strip():
            continue
        cells = line.split("\t")
        if len(cells) != len(header):
            errors.append(f"línea {number}: {len(cells)} columnas, se esperaban {len(header)}")
            continue
        key = cells[0]
        if key in seen:
            errors.append(f"línea {number}: clave repetida «{key}»")
        seen.add(key)
        for lang, value in zip(header[1:], cells[1:]):
            if not value.strip():
                errors.append(f"línea {number}: «{key}» sin traducción a {lang}")
        # Los marcadores de formato deben ser los mismos en todos los idiomas.
        markers = {tuple(sorted(re.findall(r"%\d\$[sd]", v))) for v in cells[1:]}
        if len(markers) > 1:
            errors.append(f"línea {number}: «{key}» no usa los mismos marcadores %n$ en todos los idiomas")
        rows.append(cells)
    if errors:
        sys.exit("strings.tsv no es válido:\n  " + "\n  ".join(errors))
    return header, rows


def render(rows, column: int) -> str:
    body = "".join(f'    <string name="{row[0]}">{android_text(row[column])}</string>\n' for row in rows)
    return f'<?xml version="1.0" encoding="utf-8"?>\n<resources>\n{body}</resources>\n'


def main():
    header, rows = load()
    stale = []
    for index, lang in enumerate(header[1:], start=1):
        target = RES / FOLDERS[lang] / "strings.xml"
        xml = render(rows, index)
        if "--check" in sys.argv:
            if not target.exists() or target.read_text(encoding="utf-8") != xml:
                stale.append(str(target.relative_to(ROOT)))
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(xml, encoding="utf-8", newline="\n")
    if stale:
        sys.exit("Desactualizados (ejecuta gen_strings.py): " + ", ".join(stale))
    print(f"{len(rows)} cadenas en {len(FOLDERS)} idiomas")


if __name__ == "__main__":
    main()
