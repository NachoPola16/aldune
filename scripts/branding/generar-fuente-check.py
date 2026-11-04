#!/usr/bin/env python3
"""Genera AlduneCheck, la fuente de las casillas de tarea (☐ y ☒).

Uso:  python scripts/branding/generar-fuente-check.py
Requiere: pip install fonttools shapely

El texto guardado no cambia (siguen siendo ☐ y ☒; ☑ real se descartó porque Segoe lo dibuja como un
cuadrado negro, ver TaskLines.cs). Esta fuente solo cambia cómo se DIBUJAN: ☒ lleva un tick en vez de
una aspa. Tiene la caja de Segoe UI Symbol (la fuente de la que Windows sacaba ☐ y ☒) y las métricas de línea de la
fuente de cada plataforma, de modo que ni el interlineado ni la zona de clic de NoteWindow.FindCheckboxZoneAt se mueven.
Dibujada a mano (no derivada de Segoe, que no se puede redistribuir). Se escribe en dos sitios: como
recurso de WPF y como recurso de fuente de Android, cada uno en tamaño normal y grande.
"""
import os
from pathlib import Path

# Fecha fija en la cabecera de la fuente: así regenerar no cambia los bytes si el dibujo no cambia.
os.environ.setdefault("SOURCE_DATE_EPOCH", "1793000000")

from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
from shapely import affinity
from shapely.geometry import LineString, box
from shapely.geometry.polygon import orient

RAIZ = Path(__file__).resolve().parents[2]
# (destino, ascendente, descendente): cada plataforma lleva las métricas de línea de la fuente con la que convive,
# porque si la de la casilla fuera más alta, las líneas con casilla saldrían más altas que las demás.
# La última columna baja el dibujo (unidades de em/2048): con la caja grande en Windows, anclada a la línea de base, su centro quedaba
# ~2 px por encima de la flecha «→» de una viñeta y del texto; 250 la deja a ~0,2 px (medido con la sonda probe72).
# Cada destino se escribe también en versión «grande» (ajuste «Casillas grandes»): la misma caja un 25 % mayor, con su avance.
GRANDE = 1.25
# Normal Windows box lowered by 100 (2026-11-04): anchored to the baseline its centre sat level with the capitals but ~1.5 px above the middle of
# lowercase letters (x-height), so it read as high on ordinary text. At 100 it is +1.0 px from the x-height centre and -0.5 px from the capitals' centre,
# 0.5 px under the baseline (measured at 15 px with an in-process render of six fonts).
DESTINOS = [
    (RAIZ / "src" / "Aldune" / "Assets" / "AlduneCheck.ttf", "AlduneCheck", 1.0, 2210, 514, 100),  # Segoe UI Symbol / Variable; 100: see the note below
    (RAIZ / "src" / "Aldune" / "Assets" / "AlduneCheckLarge.ttf", "AlduneCheckLarge", GRANDE, 2210, 514, 250),
    (RAIZ / "android" / "app" / "src" / "main" / "res" / "font" / "aldune_check.ttf", "AlduneCheck", 1.0, 1900, 500, 0),  # Roboto
    (RAIZ / "android" / "app" / "src" / "main" / "res" / "font" / "aldune_check_large.ttf", "AlduneCheckLarge", GRANDE, 1900, 500, 0),
]

EM = 2048
AVANCE = 1764
X0, X1, Y0, Y1 = 165, 1599, 0, 1434  # caja de tinta de Segoe: 0,093..0,907 del avance
GROSOR = 124  # el grosor del trazo de la caja de Segoe


def _anillo(coords):
    return [(round(x), round(y)) for x, y in coords][:-1]


def _dibuja(poligono):
    """Contorno exterior en sentido horario y huecos en antihorario (relleno 'no cero' de TrueType)."""
    pen = TTGlyphPen(None)
    for parte in getattr(poligono, "geoms", [poligono]):  # la caja y el tick son piezas separadas
        parte = orient(parte, sign=-1.0)
        for anillo in [parte.exterior, *parte.interiors]:
            pts = _anillo(anillo.coords)
            pen.moveTo(pts[0])
            for p in pts[1:]:
                pen.lineTo(p)
            pen.closePath()
    return pen.glyph()


def caja_vacia():
    return box(X0, Y0, X1, Y1).difference(box(X0 + GROSOR, Y0 + GROSOR, X1 - GROSOR, Y1 - GROSOR))



def caja_con_tick():
    """La misma caja fina que ☐ con un tick dentro, del mismo grosor de trazo: una caja rellena pesaba
    mucho más que ☐ (el defecto que se descartó con ☑)."""
    tick = LineString([(430, 740), (730, 440), (1330, 1050)]).buffer(
        GROSOR / 2, cap_style="flat", join_style="mitre", mitre_limit=3
    )
    return caja_vacia().union(tick)


def escalar(forma, escala):
    return affinity.scale(forma, escala, escala, origin=(0, 0))


def bajar(forma, unidades):
    return affinity.translate(forma, 0, -unidades)


def fuente(nombre, escala, asc, desc, baja=0):
    nombres = [".notdef", "uni2610", "uni2612"]
    fb = FontBuilder(EM, isTTF=True)
    fb.setupGlyphOrder(nombres)
    fb.setupCharacterMap({0x2610: "uni2610", 0x2611: "uni2612", 0x2612: "uni2612"})
    vacio = TTGlyphPen(None).glyph()
    fb.setupGlyf({".notdef": vacio, "uni2610": _dibuja(bajar(escalar(caja_vacia(), escala), baja)), "uni2612": _dibuja(bajar(escalar(caja_con_tick(), escala), baja))})
    fb.setupHorizontalMetrics({n: (round(AVANCE * escala), round(X0 * escala)) if n != ".notdef" else (round(AVANCE * escala), 0) for n in nombres})
    fb.setupHorizontalHeader(ascent=asc, descent=-desc)
    fb.setupNameTable({"familyName": nombre, "styleName": "Regular"})
    fb.setupOS2(sTypoAscender=asc, sTypoDescender=-desc, sTypoLineGap=0, usWinAscent=asc, usWinDescent=desc)
    fb.setupPost()
    return fb


def main():
    for destino, nombre, escala, asc, desc, baja in DESTINOS:
        destino.parent.mkdir(parents=True, exist_ok=True)
        fuente(nombre, escala, asc, desc, baja).save(destino)
        print("escrito", destino.relative_to(RAIZ))


if __name__ == "__main__":
    main()
