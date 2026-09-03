#!/usr/bin/env python3
"""Genere Aetheria.AppDir/aetheria.png (256x256) sans dependance externe.

appimagetool exige une icone a la racine de l'AppDir correspondant a `Icon=` du
.desktop. On n'a ni ImageMagick ni Pillow sur la machine de build Windows du
projet -> on ecrit un PNG RGBA a la main (zlib est dans la stdlib).

Rendu : fond acier tres sombre + gros "A" stylise (deux jambes + barre) dans le
rouge d'accent du Launcher (#a8353a), facon logo de la barre laterale.
"""
import struct
import zlib
import os

SIZE = 256
BG = (26, 20, 18, 255)        # #1a1412 acier sombre
FG = (168, 53, 58, 255)       # #a8353a rouge d'accent
EDGE = (214, 170, 120, 255)   # liseré chaud discret


def inside_A(x, y):
    # Coordonnees normalisees 0..1, origine en haut a gauche.
    nx, ny = x / SIZE, y / SIZE
    # Marges
    if ny < 0.14 or ny > 0.90 or nx < 0.10 or nx > 0.90:
        return False
    # Deux jambes du A : x du bord gauche/droit a hauteur ny
    # apex en haut au centre (0.5), base large en bas
    spread = 0.42 * (ny - 0.12) / 0.80
    left_outer = 0.5 - spread - 0.11
    left_inner = 0.5 - spread + 0.02
    right_inner = 0.5 + spread - 0.02
    right_outer = 0.5 + spread + 0.11
    on_leg = (left_outer <= nx <= left_inner) or (right_inner <= nx <= right_outer)
    # Barre horizontale
    on_bar = (0.52 <= ny <= 0.64) and (left_inner - 0.02 <= nx <= right_inner + 0.02)
    return on_leg or on_bar


def build_png(path):
    raw = bytearray()
    for y in range(SIZE):
        raw.append(0)  # filtre 0 par scanline
        for x in range(SIZE):
            if inside_A(x, y):
                # petit liseré : pixels de bord du glyphe
                r, g, b, a = FG
                if not inside_A(x - 2, y) or not inside_A(x + 2, y) or not inside_A(x, y - 2) or not inside_A(x, y + 2):
                    r, g, b, a = EDGE
                raw += bytes((r, g, b, a))
            else:
                raw += bytes(BG)

    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    ihdr = struct.pack(">IIBBBBB", SIZE, SIZE, 8, 6, 0, 0, 0)  # 8-bit RGBA
    png = (b"\x89PNG\r\n\x1a\n"
           + chunk(b"IHDR", ihdr)
           + chunk(b"IDAT", zlib.compress(bytes(raw), 9))
           + chunk(b"IEND", b""))
    with open(path, "wb") as fh:
        fh.write(png)
    print(f"Wrote {path} ({len(png)} bytes)")


if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))
    build_png(os.path.join(here, "Aetheria.AppDir", "aetheria.png"))
