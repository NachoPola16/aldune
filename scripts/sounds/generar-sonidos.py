"""Genera los sonidos de papel y pluma de Aldune (WAV mono, 16 bits, 22050 Hz, cabecera de 44 bytes).

Todo sale de ruido filtrado y envolventes: sin ficheros ni licencias de terceros. La semilla es fija, así que
volver a ejecutarlo da los mismos bytes. Salida: src/Aldune/Resources/Sounds/*.wav (incrustados en Windows) y
android/app/src/main/res/raw/*.wav. Los nombres siguen a SoundChoice: pagina, pliegue, toque, arruga, rasguno.

Uso: python scripts/sounds/generar-sonidos.py
"""
import math
import random
import struct
import wave
from pathlib import Path

RATE = 22050
RAIZ = Path(__file__).resolve().parents[2]
DESTINOS = [RAIZ / "src/Aldune/Resources/Sounds", RAIZ / "android/app/src/main/res/raw"]


def ruido(n, rng):
    return [rng.uniform(-1, 1) for _ in range(n)]


def paso_banda(x, bajo, alto):
    """Dos filtros de un polo: pasa-bajos en `alto` menos pasa-bajos en `bajo`."""
    a_alto = 1 - math.exp(-2 * math.pi * alto / RATE)
    a_bajo = 1 - math.exp(-2 * math.pi * bajo / RATE)
    y1 = y2 = 0.0
    out = []
    for v in x:
        y1 += a_alto * (v - y1)
        y2 += a_bajo * (v - y2)
        out.append(y1 - y2)
    return out


def env(t, ataque, caida):
    """Subida lineal corta y caída exponencial."""
    return (t / ataque if t < ataque else 1.0) * math.exp(-max(0.0, t - ataque) / caida)


def normalizar(x, pico):
    m = max(abs(v) for v in x) or 1.0
    return [v / m * pico for v in x]


def pagina(rng):
    """Pasar hoja: barrido de ruido que sube y se apaga, con un roce final."""
    n = int(RATE * 0.38)
    base = paso_banda(ruido(n, rng), 600, 5200)
    return normalizar([v * env(i / RATE, 0.06, 0.11) * (0.6 + 0.4 * math.sin(math.pi * i / n)) for i, v in enumerate(base)], 0.5)


def pliegue(rng):
    """Papel que se pliega: dos roces cortos y un marcado seco."""
    n = int(RATE * 0.3)
    out = [0.0] * n
    for inicio, fuerza, banda in [(0.0, 0.7, (900, 4500)), (0.11, 0.5, (700, 3800)), (0.2, 0.9, (1400, 6000))]:
        k = int(RATE * 0.07)
        trozo = paso_banda(ruido(k, rng), *banda)
        for i, v in enumerate(trozo):
            j = int(inicio * RATE) + i
            if j < n:
                out[j] += v * fuerza * env(i / RATE, 0.004, 0.018)
    return normalizar(out, 0.5)


def toque(rng):
    """Toque de pluma: golpe breve y apagado con un cuerpo grave."""
    n = int(RATE * 0.12)
    cuerpo = [math.sin(2 * math.pi * 210 * i / RATE) * env(i / RATE, 0.001, 0.025) for i in range(n)]
    clic = paso_banda(ruido(n, rng), 1500, 6000)
    return normalizar([c * 0.7 + k * env(i / RATE, 0.0008, 0.006) * 0.8 for i, (c, k) in enumerate(zip(cuerpo, clic))], 0.45)


def arruga(rng):
    """Papel que se arruga: muchas crepitaciones breves repartidas."""
    n = int(RATE * 0.55)
    out = [0.0] * n
    for _ in range(46):
        inicio = int(rng.uniform(0, 0.45) * RATE)
        fuerza = rng.uniform(0.25, 1.0) * (1 - inicio / n * 0.5)
        k = int(RATE * rng.uniform(0.006, 0.02))
        trozo = paso_banda(ruido(k, rng), rng.uniform(800, 2000), rng.uniform(3500, 8000))
        for i, v in enumerate(trozo):
            if inicio + i < n:
                out[inicio + i] += v * fuerza * env(i / RATE, 0.001, 0.006)
    return normalizar(out, 0.5)


def rasguno(rng):
    """Rasguño corto de pluma: ruido agudo muy breve. Suena al escribir, así que es discreto."""
    n = int(RATE * 0.06)
    base = paso_banda(ruido(n, rng), 2500, 7500)
    return normalizar([v * env(i / RATE, 0.004, 0.018) for i, v in enumerate(base)], 0.28)


SONIDOS = {"pagina": pagina, "pliegue": pliegue, "toque": toque, "arruga": arruga, "rasguno": rasguno}


def escribir(ruta, muestras):
    with wave.open(str(ruta), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, v)) * 32767)) for v in muestras))


def main():
    for nombre, fn in SONIDOS.items():
        muestras = fn(random.Random(hash_estable(nombre)))
        for d in DESTINOS:
            d.mkdir(parents=True, exist_ok=True)
            escribir(d / f"aldune_{nombre}.wav", muestras)
        print(f"{nombre}: {len(muestras) / RATE:.2f} s")


def hash_estable(texto):
    return sum(ord(c) * 31 ** i for i, c in enumerate(texto)) % 2**31


if __name__ == "__main__":
    main()
