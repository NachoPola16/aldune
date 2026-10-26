#!/usr/bin/env python3
"""Deriva todos los iconos de Aldune desde UN solo símbolo (la «A» de tres notas).

Uso:  python scripts/branding/generar-iconos.py [--solo-comprobar]
Requiere: pip install shapely pillow

El símbolo se define aquí, una vez, como geometría real (polígonos sin solapes: los huecos entre
notas son huecos de verdad, no un trazo del color del fondo). Eso permite que el mismo dibujo sirva
para SVG, VectorDrawable de Android (incluido el monocromo, que no admite trazos "de fondo") y PNG.
Ver docs/BRANDING.md.
"""
import math
import struct
import sys
from io import BytesIO
from pathlib import Path

from PIL import Image, ImageDraw
from shapely.geometry import LineString, Point
from shapely.ops import unary_union

RAIZ = Path(__file__).resolve().parents[2]

# Paleta de la marca (tema Clásico). El fondo es el marrón oscuro del logo de siempre.
FONDO = "#2A261F"
AMARILLO = "#EBD38B"
CIAN = "#83E7F2"
ROSA = "#F9BEF2"

C = 54.0  # centro del lienzo de 108 (la unidad del icono adaptativo de Android)


def _rot(p, ang_deg):
    """Gira p alrededor del centro del lienzo, en sentido horario visual (eje y hacia abajo)."""
    a = math.radians(ang_deg)
    dx, dy = p[0] - C, p[1] - C
    return (C + dx * math.cos(a) - dy * math.sin(a), C + dx * math.sin(a) + dy * math.cos(a))


def simbolo(grosor=13.0, hueco=1.2, barra=10.0, largo=56.0, angulo=22.4, centro_patas=9.0, altura_barra=11.0):
    """Las tres notas de la «A», en un lienzo de 108 centrado en (54, 54).

    Devuelve [(color, polígono shapely)]. Orden de pintado: pata amarilla, pata cian, travesaño rosa;
    cada una recorta a las de debajo con `hueco` de separación.
    """
    medio = largo / 2 - grosor / 2

    def pata(signo):
        # La pata vertical centrada en x = C ∓ centro_patas, girada alrededor de su propio punto medio.
        cx = C - signo * centro_patas
        ext = [(cx, C - medio), (cx, C + medio)]
        ang = signo * angulo
        a = math.radians(ang)
        pts = []
        for x, y in ext:
            dx, dy = x - cx, y - C
            pts.append((cx + dx * math.cos(a) - dy * math.sin(a), C + dx * math.sin(a) + dy * math.cos(a)))
        return LineString(pts).buffer(grosor / 2, quad_segs=24)

    izq, der = pata(+1), pata(-1)
    bx = (C - 14 + barra / 2, C + 14 - barra / 2)
    bar = LineString([(bx[0], C + altura_barra), (bx[1], C + altura_barra)]).buffer(barra / 2, quad_segs=24)

    # La cian va encima de la amarilla (la recorta) y el travesaño encima de las dos.
    hueco_bar = bar.buffer(hueco, quad_segs=24)
    amarilla = izq.difference(der.buffer(hueco, quad_segs=24)).difference(hueco_bar)
    cian = der.difference(hueco_bar)
    return [(AMARILLO, amarilla), (CIAN, cian), (ROSA, bar)]


def escalar(piezas, k, cx=C, cy=C):
    from shapely import affinity
    return [(c, affinity.scale(p, k, k, origin=(cx, cy))) for c, p in piezas]


def trasladar(piezas, dx, dy):
    from shapely import affinity
    return [(c, affinity.translate(p, dx, dy)) for c, p in piezas]


def d_path(poligono, decimales=2):
    """pathData con solo M/L/Z (vale tanto en SVG como en VectorDrawable)."""
    geoms = list(poligono.geoms) if poligono.geom_type == "MultiPolygon" else [poligono]
    trozos = []
    for g in geoms:
        g = g.simplify(0.02)
        for anillo in [g.exterior, *g.interiors]:
            pts = list(anillo.coords)[:-1]
            f = lambda v: f"{v:.{decimales}f}".rstrip("0").rstrip(".")
            trozos.append("M" + " ".join(f"{f(x)} {f(y)}" for x, y in pts) + "Z")
    return "".join(trozos)


# --- Composiciones -----------------------------------------------------------------------------

# Android adaptativo: el símbolo cabe en el círculo de 66 dp de la zona segura.
K_ADAPTATIVO = 0.92
# Ficha de Windows y de las tiendas: el contenedor es un cuadrado redondeado, sobra más sitio.
K_FICHA = 1.16
FICHA_MARGEN = 3.4      # margen de la ficha respecto al lienzo de 108 (igual que el logo anterior: 8/256)
FICHA_RADIO = 23.7      # esquina (56/256 del logo anterior)


def radio_max(piezas):
    return max(math.hypot(x - C, y - C) for _, p in piezas for x, y in
               (p.geoms[0].exterior.coords if p.geom_type == "MultiPolygon" else p.exterior.coords))


# --- Rasterizado -------------------------------------------------------------------------------

def hex_rgb(h, a=255):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def rasterizar(piezas, px, *, fondo=None, forma="ficha", super_=8, mono=None):
    """PNG RGBA de px×px. `forma`: 'ficha' (cuadrado redondeado), 'cuadrado' o 'libre' (sin fondo)."""
    n = px * super_
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    s = n / 108.0
    if forma == "ficha" and fondo:
        m = FICHA_MARGEN * s
        d.rounded_rectangle([m, m, n - m - 1, n - m - 1], radius=FICHA_RADIO * s, fill=hex_rgb(fondo))
    elif forma == "cuadrado" and fondo:
        d.rectangle([0, 0, n, n], fill=hex_rgb(fondo))
    for color, poly in piezas:
        for g in (poly.geoms if poly.geom_type == "MultiPolygon" else [poly]):
            d.polygon([(x * s, y * s) for x, y in g.exterior.coords], fill=hex_rgb(mono or color))
    return img.resize((px, px), Image.LANCZOS)


def escribir_ico(ruta, marcos):
    """.ico con marcos PNG (igual que el anterior): [(px, Image)]."""
    datos = []
    for px, im in marcos:
        b = BytesIO()
        im.save(b, "PNG", optimize=True)
        datos.append((px, b.getvalue()))
    cab = struct.pack("<HHH", 0, 1, len(datos))
    off = 6 + 16 * len(datos)
    ent, cuerpo = b"", b""
    for px, d in datos:
        ent += struct.pack("<BBBBHHII", px % 256, px % 256, 0, 0, 1, 32, len(d), off + len(cuerpo))
        cuerpo += d
    Path(ruta).write_bytes(cab + ent + cuerpo)


# --- Salidas -----------------------------------------------------------------------------------

def svg_simbolo(piezas, cabecera=""):
    cuerpo = "\n".join(f'  <path fill="{c}" d="{d_path(p)}"/>' for c, p in piezas)
    return f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 108 108">\n{cabecera}{cuerpo}\n</svg>\n'


def svg_ficha(piezas, cabecera=""):
    m = FICHA_MARGEN
    fondo = (f'  <rect x="{m}" y="{m}" width="{108 - 2 * m:.1f}" height="{108 - 2 * m:.1f}" '
             f'rx="{FICHA_RADIO}" fill="{FONDO}"/>\n')
    cuerpo = "\n".join(f'  <path fill="{c}" d="{d_path(p)}"/>' for c, p in piezas)
    return f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 108 108">\n{cabecera}{fondo}{cuerpo}\n</svg>\n'


def vector_android(piezas, nombre_comentario, color_fijo=None, dp=108, viewport=108, desplaz=(0, 0)):
    grupo_ini = ""
    grupo_fin = ""
    if desplaz != (0, 0):
        grupo_ini = f'    <group android:translateX="{desplaz[0]}" android:translateY="{desplaz[1]}">\n'
        grupo_fin = "    </group>\n"
    paths = ""
    for c, p in piezas:
        col = color_fijo or c
        paths += (f'    <path android:fillColor="{col}"\n        android:pathData="{d_path(p)}" />\n')
    return (f'<?xml version="1.0" encoding="utf-8"?>\n{nombre_comentario}'
            f'<vector xmlns:android="http://schemas.android.com/apk/res/android"\n'
            f'    android:width="{dp}dp" android:height="{dp}dp" '
            f'android:viewportWidth="{viewport}" android:viewportHeight="{viewport}">\n'
            f'{grupo_ini}{paths}{grupo_fin}</vector>\n')


def main():
    base = simbolo()
    adaptativo = escalar(base, K_ADAPTATIVO)
    r = radio_max(adaptativo)
    print(f"radio máximo en el icono adaptativo: {r:.2f} de 33 (zona segura 66 dp)")
    if r > 33:
        sys.exit("el símbolo se sale de la zona segura")
    if "--solo-comprobar" in sys.argv:
        return

    out = RAIZ / "docs" / "branding"
    out.mkdir(parents=True, exist_ok=True)

    ficha = escalar(base, K_FICHA)  # símbolo mayor dentro de la ficha de Windows (sin máscara que lo recorte)
    # Para 16-24 px: más grueso y con huecos algo más anchos, para que no se pierdan al reducir.
    pequeno = escalar(simbolo(grosor=15.0, hueco=2.0, barra=12.0, largo=60.0, angulo=21.0), K_FICHA)

    # SVG maestros
    (out / "aldune-symbol.svg").write_text(
        svg_simbolo(adaptativo, "  <!-- Símbolo de Aldune: la «A» de tres notas. Maestro; todo se deriva de él. Ver docs/BRANDING.md. -->\n"),
        encoding="utf-8")
    (RAIZ / "src/Aldune/Assets/aldune-logo.svg").write_text(
        svg_ficha(ficha, "  <!-- Ficha de Windows: el símbolo (docs/branding/aldune-symbol.svg) sobre fondo oscuro. aldune.ico sale de aquí\n       y de aldune-logo-small.svg para 16-24 px. Se regenera con scripts/branding/generar-iconos.py. -->\n"),
        encoding="utf-8")
    (RAIZ / "src/Aldune/Assets/aldune-logo-small.svg").write_text(
        svg_ficha(pequeno, "  <!-- Versión para 16-24 px: trazo más grueso y huecos más anchos, para que se lea en la bandeja. -->\n"),
        encoding="utf-8")

    # .ico de Windows (los tamaños de siempre: 16-24 con la versión gruesa)
    marcos = []
    for px in (16, 20, 24, 32, 40, 48, 64, 128, 256):
        piezas = pequeno if px <= 24 else ficha
        marcos.append((px, rasterizar(piezas, px, fondo=FONDO, forma="ficha")))
    escribir_ico(RAIZ / "src/Aldune/Assets/aldune.ico", marcos)

    # Tiendas y repositorio
    fastlane = RAIZ / "android/fastlane/metadata/android/en-US/images/icon.png"
    rasterizar(adaptativo, 512, fondo=FONDO, forma="cuadrado").save(fastlane, optimize=True)
    rasterizar(ficha, 512, fondo=FONDO, forma="ficha").save(out / "aldune-logo-512.png", optimize=True)
    rasterizar(ficha, 128, fondo=FONDO, forma="ficha").save(out / "aldune-logo-128.png", optimize=True)

    # Favicon: el símbolo sobre su ficha, 16/32/48
    escribir_ico(out / "favicon.ico", [(px, rasterizar(pequeno if px <= 24 else ficha, px, fondo=FONDO, forma="ficha")) for px in (16, 32, 48)])
    (out / "favicon.svg").write_text(svg_ficha(pequeno, ""), encoding="utf-8")

    # Android
    res = RAIZ / "android/app/src/main/res/drawable"
    (res / "ic_launcher_foreground.xml").write_text(vector_android(
        adaptativo,
        "<!-- La «A» de tres notas (docs/BRANDING.md) dentro de la zona segura de 66 dp del icono adaptativo de 108 dp, sin borde.\n"
        "     Generado por scripts/branding/generar-iconos.py: no se edita a mano. -->\n"), encoding="utf-8")
    (res / "ic_launcher_monochrome.xml").write_text(vector_android(
        adaptativo,
        "<!-- Versión de un solo color para los iconos con tema (Android 13+); el sistema la tiñe. Los huecos entre notas son\n"
        "     huecos reales de la geometría. Generado por scripts/branding/generar-iconos.py. -->\n",
        color_fijo="#FFFFFFFF"), encoding="utf-8")

    # Icono pequeño de notificaciones (24 dp, solo silueta: Android lo tiñe y descarta el color). Versión gruesa.
    # El viewport sigue siendo de 108: el VectorDrawable lo reduce a 24 dp. Sin máscara, el símbolo puede ocupar más.
    estado = escalar(simbolo(grosor=15.0, hueco=2.0, barra=12.0, largo=60.0, angulo=21.0), 1.25)
    (res / "ic_stat_aldune.xml").write_text(vector_android(
        estado,
        "<!-- Icono de la barra de estado: la «A» en una sola tinta, versión gruesa para que aguante a 24 dp.\n"
        "     Generado por scripts/branding/generar-iconos.py. -->\n",
        color_fijo="#FFFFFFFF", dp=24), encoding="utf-8")

    # Acceso directo de pulsación larga (48 dp): el símbolo sobre un círculo del color de fondo
    circulo = (f'    <path android:fillColor="{FONDO}"\n'
               f'        android:pathData="M54,54m-54,0a54,54 0,1 1,108 0a54,54 0,1 1,-108 0" />\n')
    cuerpo = "".join(f'    <path android:fillColor="{c}"\n        android:pathData="{d_path(p)}" />\n' for c, p in adaptativo)
    (res / "ic_shortcut_new_note.xml").write_text(
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<!-- El acceso directo del icono: la «A» de tres notas sobre el fondo del logo (docs/BRANDING.md).\n'
        '     Generado por scripts/branding/generar-iconos.py. -->\n'
        '<vector xmlns:android="http://schemas.android.com/apk/res/android"\n'
        '    android:width="48dp" android:height="48dp" android:viewportWidth="108" android:viewportHeight="108">\n'
        f'{circulo}{cuerpo}</vector>\n', encoding="utf-8")

    # Símbolo suelto para el widget (vista previa del selector): el mismo, en color
    (res / "ic_aldune_symbol.xml").write_text(vector_android(
        escalar(base, 1.1),
        "<!-- La «A» de tres notas, sin fondo, para la vista previa del widget. Generado por scripts/branding/generar-iconos.py. -->\n",
        dp=48), encoding="utf-8")
    print("hecho")


if __name__ == "__main__":
    main()
