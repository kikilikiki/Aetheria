"""
Concepts visuels générés par code (voir Docs/Idees.md — "sprites/textures réels", mis de côté
côté moteur faute d'un vrai pipeline d'art, mais illustré ici en mockups pixel-art simples pour
montrer à quoi le rendu final pourrait ressembler). Pas du vrai art : des blocs de couleur
géométriques façon pixel-art grossier, à charge d'un·e artiste de les remplacer.

Réutilise les palettes déjà définies dans le code (Client/World/WorldMap.cs, KingdomBiome.cs,
Server/Persistence/MonsterCatalogSeeder.cs) pour rester cohérent avec les couleurs déjà en jeu.
"""

from PIL import Image, ImageDraw
import os

OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "..", "..", "..", "..",
                        "Documents", "aetheria", "Docs", "Image")
OUT_DIR = os.path.abspath("/home/killian/Documents/aetheria/Docs/Image")
os.makedirs(OUT_DIR, exist_ok=True)

TRANSPARENT = (0, 0, 0, 0)


def render_grid(grid, palette, scale=16, outline=None):
    """grid: liste de chaines (une par ligne), chaque caractere indexe `palette` (' ' = transparent)."""
    h = len(grid)
    w = max(len(row) for row in grid)
    img = Image.new("RGBA", (w, h), TRANSPARENT)
    px = img.load()
    for y, row in enumerate(grid):
        for x, ch in enumerate(row):
            if ch == " ":
                continue
            px[x, y] = palette[ch]
    img = img.resize((w * scale, h * scale), Image.NEAREST)
    if outline:
        # Contour simple : bord noir semi-transparent sur les pixels non-transparents adjacents a du vide.
        draw = ImageDraw.Draw(img)
        for y in range(h):
            for x in range(w):
                ch = grid[y][x] if x < len(grid[y]) else " "
                if ch == " ":
                    continue
                neighbors = [
                    grid[y - 1][x] if y > 0 and x < len(grid[y - 1]) else " ",
                    grid[y + 1][x] if y < h - 1 and x < len(grid[y + 1]) else " ",
                    grid[y][x - 1] if x > 0 else " ",
                    grid[y][x + 1] if x < len(grid[y]) - 1 else " ",
                ]
                if " " in neighbors:
                    draw.rectangle(
                        [x * scale, y * scale, (x + 1) * scale - 1, (y + 1) * scale - 1],
                        outline=outline, width=max(1, scale // 8),
                    )
    return img


# ---------------------------------------------------------------------------
# Personnage joueur (Guerrier neutre, voir GDD - creation de personnage)
# ---------------------------------------------------------------------------
player_palette = {
    "s": (222, 184, 148, 255),   # peau
    "h": (90, 60, 40, 255),      # cheveux
    "a": (120, 130, 145, 255),   # armure claire
    "A": (75, 85, 100, 255),     # armure foncee (ombre)
    "b": (150, 110, 60, 255),    # ceinture / cuir
    "e": (40, 40, 45, 255),      # yeux / details
}
player_grid = [
    "   hhhhhh   ",
    "  hhhhhhhh  ",
    "  hssssssh  ",
    "  hsseesse  ",
    "   ssssss   ",
    "   ssssss   ",
    "  aaaaaaaa  ",
    " aaAAAAAAaa ",
    " aaAAAAAAaa ",
    " aaAbbbbAaa ",
    "  aaAAAAaa  ",
    "  aa    aa  ",
    "  aa    aa  ",
    "  AA    AA  ",
    "  AA    AA  ",
    "  bb    bb  ",
]
render_grid(player_grid, player_palette, scale=20, outline=(20, 20, 25, 200)).save(
    os.path.join(OUT_DIR, "personnage-joueur-guerrier.png"))

# ---------------------------------------------------------------------------
# Batiment - Capitale (voir Client/World/WorldMap.cs : Gold/DarkGold/Tan)
# ---------------------------------------------------------------------------
building_palette = {
    "g": (217, 178, 63, 255),    # or (toit)
    "G": (140, 112, 35, 255),    # or fonce (ombre toit)
    "w": (200, 170, 130, 255),   # mur clair
    "W": (150, 120, 85, 255),    # mur ombre
    "d": (90, 60, 35, 255),      # porte
    "f": (235, 210, 120, 255),   # fenetre allumee
}
building_grid = [
    "      gggg      ",
    "     gggggg     ",
    "    gggggggg    ",
    "   GggggggggG   ",
    "  GGGGGGGGGGGG  ",
    " wwwwwwwwwwwwww ",
    " wwwfwwwwwwfwww ",
    " wwwfwwwwwwfwww ",
    " WwwwwwwwwwwwWw ",
    " WwwwwwwwwwwwWw ",
    " WwwwwdddwwwwWw ",
    " WwwwwdddwwwwWw ",
    " WWWWWdddWWWWWW ",
]
render_grid(building_grid, building_palette, scale=20, outline=(30, 22, 10, 200)).save(
    os.path.join(OUT_DIR, "batiment-capitale.png"))

# ---------------------------------------------------------------------------
# Monstre - Braisillon (starter Feu, voir MonsterCatalogSeeder : "petite salamandre qui couve
# des braises sous ses ecailles")
# ---------------------------------------------------------------------------
monster_palette = {
    "r": (200, 70, 40, 255),     # ecailles rouge/orange
    "R": (140, 40, 20, 255),     # ombre
    "y": (250, 190, 60, 255),    # braises (ventre/dos)
    "e": (255, 255, 255, 255),   # blanc de l'oeil
    "p": (30, 20, 15, 255),      # pupille
}
monster_grid = [
    "   rrrr     ",
    "  rrrrrr r  ",
    " rreerreer  ",
    " rrpprrppr  ",
    " rrrrrrrrr  ",
    "  rryyyrr   ",
    "  RyyyyyR   ",
    "  RyyyyyR   ",
    "   Ryyy R   ",
    "  rr   rr   ",
    " RR     RR  ",
]
render_grid(monster_grid, monster_palette, scale=20, outline=(40, 15, 10, 200)).save(
    os.path.join(OUT_DIR, "monstre-braisillon.png"))

# ---------------------------------------------------------------------------
# Monstre - archetype Tank (voir Golem Rocheux, MonsterType.Tank : teintes pierre)
# ---------------------------------------------------------------------------
tank_palette = {
    "g": (140, 140, 150, 255),
    "G": (95, 95, 105, 255),
    "c": (170, 170, 180, 255),   # eclats de cristal / fissures claires
    "e": (255, 210, 90, 255),    # yeux luminescents
}
tank_grid = [
    "  GggggggG  ",
    " GgggggggggG",
    " gggGggGggg ",
    " ggg e  e gg",
    " GggggggggG ",
    "GggGgggGgggG",
    "gg  Gcc G  g",
    "gg   cc    g",
    " GggGggGggG ",
    " Gg      gG ",
    " Gg      gG ",
]
render_grid(tank_grid, tank_palette, scale=20, outline=(25, 25, 30, 200)).save(
    os.path.join(OUT_DIR, "monstre-archetype-tank.png"))

# ---------------------------------------------------------------------------
# PNJ - Villageois (voir Client/World/WorldMap.cs Npcs)
# ---------------------------------------------------------------------------
npc_palette = {
    "s": (222, 184, 148, 255),
    "h": (110, 90, 60, 255),
    "c": (120, 150, 110, 255),   # vetements
    "C": (85, 110, 78, 255),
    "e": (40, 40, 45, 255),
}
npc_grid = [
    "   hhhhh    ",
    "  hhhhhhh   ",
    "  hsssssh   ",
    "  hseesesh  ",
    "   sssss    ",
    "   ccccc    ",
    "  ccccccc   ",
    "  cCCCCCc   ",
    "  cCCCCCc   ",
    "   CC CC    ",
    "   CC CC    ",
    "   hh hh    ",
]
render_grid(npc_grid, npc_palette, scale=20, outline=(20, 20, 20, 200)).save(
    os.path.join(OUT_DIR, "pnj-villageois.png"))

print("OK - images generees dans", OUT_DIR)
for f in sorted(os.listdir(OUT_DIR)):
    print(" -", f)
