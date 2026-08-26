"""
Concepts visuels générés par code (voir Docs/Idees.md — "sprites/textures réels", mis de côté
côté moteur faute d'un vrai pipeline d'art, mais illustré ici en mockups pixel-art simples pour
montrer à quoi le rendu final pourrait ressembler). Pas du vrai art : des blocs de couleur
géométriques façon pixel-art grossier, à charge d'un·e artiste de les remplacer.

Réutilise les palettes/couleurs déjà définies dans le code pour rester cohérent avec ce qui est
déjà affiché en jeu :
- Client/World/WorldMap.cs (couleurs des PNJ "Garde royal"/"Marchande"/"Forgeron"/"Villageois")
- Client/World/CharacterAppearancePalette.cs (peau/cheveux/vêtements du joueur)
- Client/Program.cs, CombatTypeColor (une couleur par rôle de monstre — Shared/Enums/MonsterType.cs)
- Server/Persistence/MonsterCatalogSeeder.cs (Braisillon, starter Feu)

Mise à jour du 2026-08-27 (2e passe) : redessine les concepts déjà présents et couvre les 10
rôles de monstre (auparavant seul Tank avait un exemple) + davantage de PNJ/bâtiments nommés déjà
présents dans le monde, pour servir de base complète à un·e artiste.
"""

from PIL import Image, ImageDraw
import os

OUT_DIR = os.path.abspath("/home/killian/Documents/aetheria/Docs/Image")
os.makedirs(OUT_DIR, exist_ok=True)

TRANSPARENT = (0, 0, 0, 0)


def render_grid(grid, palette, scale=20, outline=None):
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


def save(grid, palette, name, scale=20, outline=(20, 20, 25, 200)):
    render_grid(grid, palette, scale=scale, outline=outline).save(os.path.join(OUT_DIR, name))


def v4_to_rgb(r, g, b):
    """Convertit un Vector4 0..1 (voir le code C#) en tuple RGB 0..255."""
    return (round(r * 255), round(g * 255), round(b * 255), 255)


def shade(rgba, factor):
    """Assombrit (factor < 1) ou eclaircit (factor > 1) une couleur RGBA, pour une ombre/un reflet cohérent."""
    r, g, b, a = rgba
    return (min(255, max(0, round(r * factor))), min(255, max(0, round(g * factor))), min(255, max(0, round(b * factor))), a)


# ===========================================================================
# Personnage joueur — 3 variantes de personnalisation (voir
# Client/World/CharacterAppearancePalette.cs pour les valeurs exactes réutilisées ici : peau,
# cheveux, vêtements). Le moteur n'a pas de "classe" de personnage (uniquement des monstres ont un
# rôle de combat) — ces 3 variantes montrent donc l'étendue de la personnalisation existante
# plutôt que 3 classes différentes.
# ===========================================================================
def player_grid(skin, skin_shadow, hair, clothes, clothes_shadow, accessory=None):
    palette = {
        "s": skin, "S": skin_shadow, "h": hair,
        "a": clothes, "A": clothes_shadow,
        "b": shade(clothes, 0.7), "e": (40, 40, 45, 255),
    }
    grid = [
        "   hhhhhh   ",
        "  hhhhhhhh  ",
        "  hssssssh  ",
        "  hsSeeSse  ",
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
    if accessory == "couronne":
        palette["k"] = (235, 200, 70, 255)
        grid[0] = "   kk  kk   "
    elif accessory == "bandeau":
        palette["k"] = (210, 60, 55, 255)
        grid[3] = "  kkkkkkkk  "
    return grid, palette


# Variante A (redo de l'originale) : peau Hâlée, cheveux Roux, vêtements Bleu.
grid, palette = player_grid(
    skin=v4_to_rgb(0.82, 0.62, 0.45), skin_shadow=v4_to_rgb(0.95, 0.90, 0.85),
    hair=v4_to_rgb(0.72, 0.32, 0.15), clothes=v4_to_rgb(0.25, 0.40, 0.72), clothes_shadow=v4_to_rgb(0.14, 0.14, 0.16))
save(grid, palette, "personnage-joueur-a.png")

# Variante B : peau Foncée, cheveux Blanc, vêtements Violet, accessoire Couronne.
grid, palette = player_grid(
    skin=v4_to_rgb(0.55, 0.38, 0.26), skin_shadow=v4_to_rgb(0.82, 0.62, 0.45),
    hair=v4_to_rgb(0.92, 0.92, 0.92), clothes=v4_to_rgb(0.52, 0.32, 0.68), clothes_shadow=v4_to_rgb(0.14, 0.14, 0.16),
    accessory="couronne")
save(grid, palette, "personnage-joueur-b.png")

# Variante C : peau Verte, cheveux Vert, vêtements Noir, accessoire Bandeau.
grid, palette = player_grid(
    skin=v4_to_rgb(0.55, 0.72, 0.45), skin_shadow=v4_to_rgb(0.92, 0.80, 0.68),
    hair=v4_to_rgb(0.30, 0.62, 0.35), clothes=v4_to_rgb(0.14, 0.14, 0.16), clothes_shadow=v4_to_rgb(0.05, 0.05, 0.06),
    accessory="bandeau")
save(grid, palette, "personnage-joueur-c.png")

# ===========================================================================
# Bâtiments (voir Client/World/BuildingInterior.cs pour les noms/rôles) — redo de la Capitale +
# 4 nouveaux (Forge, Auberge, Hôtel des ventes, Guilde), même style toit-losange que l'existant.
# ===========================================================================
def building_grid(roof, roof_shadow, wall, wall_shadow, window, door, prop_rows=None, prop_palette=None):
    """
    Voir retour utilisateur — "ajoute pour les bâtiments un truc qui les différencie comme une
    enclume à l'entrée pour la forge" : les 5 bâtiments partageaient jusqu'ici la même silhouette
    toit-losange + porte, seule la couleur changeait. `prop_rows` ajoute 2 lignes sous la porte
    avec un petit accessoire propre à chaque bâtiment (voir chaque appel ci-dessous), sur les
    mêmes colonnes que la porte (6-8 sur une rangée de 17) pour rester centré dessus.
    """
    palette = {"g": roof, "G": roof_shadow, "w": wall, "W": wall_shadow, "f": window, "d": door}
    if prop_palette:
        palette.update(prop_palette)
    grid = [
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
    if prop_rows:
        grid += prop_rows
    return grid, palette


# Capitale : deux bannières royales dressées de part et d'autre de l'entrée (hampe + étoffe qui
# retombe), en couleur or pour rester lisible sur n'importe quelle teinte de mur.
banner = (217, 178, 63, 255)
grid, palette = building_grid(
    roof=(217, 178, 63, 255), roof_shadow=(140, 112, 35, 255),
    wall=(200, 170, 130, 255), wall_shadow=(150, 120, 85, 255),
    window=(235, 210, 120, 255), door=(90, 60, 35, 255),
    prop_rows=[
        "  bb        bb  ",
        "  bB        Bb  ",
    ],
    prop_palette={"b": banner, "B": shade(banner, 0.7)})
save(grid, palette, "batiment-capitale.png", outline=(30, 22, 10, 200))

# Forge (voir "Apprenti forgeron") : enclume posée devant l'entrée (base large qui se resserre,
# reflet clair sur la table de frappe).
anvil = (75, 75, 85, 255)
grid, palette = building_grid(
    roof=(90, 90, 95, 255), roof_shadow=(55, 55, 60, 255),
    wall=(150, 120, 100, 255), wall_shadow=(110, 85, 70, 255),
    window=(235, 130, 60, 255), door=(60, 45, 35, 255),
    prop_rows=[
        "     nNNNNn     ",
        "       NN       ",
    ],
    prop_palette={"n": shade(anvil, 1.3), "N": anvil})
save(grid, palette, "batiment-forge.png", outline=(20, 15, 12, 200))

# Auberge (voir "Aubergiste") : tonneau posé à côté de l'entrée (couvercle clair, cerclages plus
# sombres pour lire "tonneau" plutôt qu'un simple carré).
barrel = (140, 95, 48, 255)
grid, palette = building_grid(
    roof=(160, 70, 45, 255), roof_shadow=(105, 42, 26, 255),
    wall=(210, 180, 140, 255), wall_shadow=(160, 130, 95, 255),
    window=(250, 220, 140, 255), door=(90, 60, 35, 255),
    prop_rows=[
        "  tTt           ",
        "  TtT           ",
    ],
    prop_palette={"t": shade(barrel, 1.2), "T": shade(barrel, 0.75)})
save(grid, palette, "batiment-auberge.png", outline=(35, 18, 10, 200))

# Hôtel des ventes (voir "Commis") : pile de pièces d'or à côté de l'entrée (silhouette
# triangulaire pour bien lire "pile", pas un bloc plein).
coin = (230, 195, 70, 255)
grid, palette = building_grid(
    roof=(60, 130, 120, 255), roof_shadow=(35, 90, 82, 255),
    wall=(215, 210, 190, 255), wall_shadow=(165, 160, 140, 255),
    window=(255, 235, 160, 255), door=(70, 55, 40, 255),
    prop_rows=[
        "             k  ",
        "            kKk ",
    ],
    prop_palette={"k": coin, "K": shade(coin, 0.75)})
save(grid, palette, "batiment-hotel-des-ventes.png", outline=(15, 25, 22, 200))

# Guilde (voir "Archiviste") : emblème en pennant suspendu au-dessus de l'entrée (pointe vers le
# bas), forme distincte de l'enclume/tonneau/pile ci-dessus.
emblem = (225, 195, 235, 255)
grid, palette = building_grid(
    roof=(120, 70, 140, 255), roof_shadow=(80, 45, 95, 255),
    wall=(190, 175, 195, 255), wall_shadow=(140, 125, 145, 255),
    window=(230, 200, 240, 255), door=(60, 40, 65, 255),
    prop_rows=[
        "      mmm       ",
        "       M        ",
    ],
    prop_palette={"m": emblem, "M": shade(emblem, 0.75)})
save(grid, palette, "batiment-guilde.png", outline=(25, 15, 28, 200))

# ===========================================================================
# Monstres — starter Braisillon (redo) + un exemple par rôle de combat (10 rôles, voir
# Shared/Enums/MonsterType.cs). Couleur de base = CombatTypeColor (Client/Program.cs) pour chaque
# rôle, silhouette distincte (oreilles/cornes/ailes/halo/etc.) pour rester lisible en petit format.
# ===========================================================================
monster_palette_braisillon = {
    "r": (200, 70, 40, 255), "R": (140, 40, 20, 255),
    "y": (250, 190, 60, 255), "e": (255, 255, 255, 255), "p": (30, 20, 15, 255),
}
monster_grid_braisillon = [
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
save(monster_grid_braisillon, monster_palette_braisillon, "monstre-braisillon.png", outline=(40, 15, 10, 200))


def role_palette(base_rgb):
    base = base_rgb
    shadow = shade(base, 0.68)
    return base, shadow


BODY_LOWER = [
    "  AaaaaaaA  ",
    " AaaaaaaaaA ",
    " AaaaaaaaaA ",
    "  AAA AAA   ",
    " aa     aa  ",
    "AA       AA ",
]

# Guerrier (rouille/orange, CombatTypeColor 0.82/0.4/0.22) : tête ronde + petites défenses.
a, A = role_palette(v4_to_rgb(0.82, 0.40, 0.22))
palette = {"a": a, "A": A, "e": (255, 255, 255, 255), "p": (30, 20, 15, 255), "t": (230, 220, 200, 255)}
grid = [
    "   aaaaaa   ",
    "  aaaaaaaa  ",
    " taaeeaeeat ",
    " aappaappa  ",
    " Aaaaaaaaa  ",
] + BODY_LOWER
save(grid, palette, "monstre-role-guerrier.png")

# Archer (vert, 0.38/0.72/0.36) : grandes oreilles dressées, silhouette agile.
a, A = role_palette(v4_to_rgb(0.38, 0.72, 0.36))
palette = {"a": a, "A": A, "e": (255, 255, 255, 255), "p": (30, 20, 15, 255), "l": shade(a, 1.25)}
grid = [
    " l      l   ",
    " ll    ll   ",
    "  llllll    ",
    "  aaaaaa    ",
    " aaeeaeea   ",
    " Aappaappa  ",
] + BODY_LOWER
save(grid, palette, "monstre-role-archer.png")

# Soigneur (jaune, 0.92/0.84/0.4) : rond, halo/croix lumineuse au-dessus de la tête.
a, A = role_palette(v4_to_rgb(0.92, 0.84, 0.40))
palette = {"a": a, "A": A, "e": (255, 255, 255, 255), "p": (30, 20, 15, 255), "h": (255, 250, 210, 255)}
grid = [
    "    hh      ",
    "   aaaaaa   ",
    "  aaaaaaaa  ",
    "  aaeeeeaa  ",
    "  Aappppa   ",
] + BODY_LOWER
save(grid, palette, "monstre-role-soigneur.png")

# Tank (redo, gris-bleu 0.45/0.45/0.55) : tête blocs/cristal, yeux luminescents, silhouette massive.
a, A = role_palette(v4_to_rgb(0.45, 0.45, 0.55))
palette = {"a": a, "A": A, "c": shade(a, 1.35), "e": (255, 210, 90, 255)}
grid = [
    "  AaaaaaaA  ",
    " AaaaaaaaaA ",
    " aaaAaaAaa  ",
    " aaa e  e aa",
    " Aaaaaaaaa  ",
    "AaaAaaaAaaaA",
    "aa  Acc A  a",
    "aa   cc    a",
    " AaaAaaAaaA ",
    " Aa      aA ",
    " Aa      aA ",
]
save(grid, palette, "monstre-role-tank.png")

# Mage (violet, 0.45/0.35/0.85) : capuche pointue, orbe flottant.
a, A = role_palette(v4_to_rgb(0.45, 0.35, 0.85))
palette = {"a": a, "A": A, "e": (255, 255, 255, 255), "p": (30, 20, 15, 255), "o": (220, 200, 255, 255)}
grid = [
    "     o      ",
    "   aaaaa    ",
    "  aaaaaaa   ",
    "  aaeeaea   ",
    "  Aappapa   ",
] + BODY_LOWER
save(grid, palette, "monstre-role-mage.png")

# Assassin (charbon/violet sombre, 0.3/0.28/0.34) : capuche, yeux rouges perçants.
a, A = role_palette(v4_to_rgb(0.30, 0.28, 0.34))
palette = {"a": a, "A": A, "r": (230, 40, 40, 255), "k": shade(a, 1.4)}
grid = [
    " k      k   ",
    " kk    kk   ",
    "  aaaaaa    ",
    "  arraraa   ",
    "  Aaaaaaa   ",
] + BODY_LOWER
save(grid, palette, "monstre-role-assassin.png")

# Support (menthe/turquoise, 0.5/0.85/0.75) : petites ailes + halo.
a, A = role_palette(v4_to_rgb(0.50, 0.85, 0.75))
palette = {"a": a, "A": A, "e": (255, 255, 255, 255), "p": (30, 20, 15, 255), "h": (240, 255, 250, 255), "w": shade(a, 1.2)}
grid = [
    "    hh      ",
    " w aaaaaa w ",
    " wwaaaaaaww ",
    "  aaeeeeaa  ",
    "  Aappppa   ",
] + BODY_LOWER
save(grid, palette, "monstre-role-support.png")

# Invocateur (magenta/prune, 0.65/0.35/0.65) : robe + rune au front + anneaux flottants.
a, A = role_palette(v4_to_rgb(0.65, 0.35, 0.65))
palette = {"a": a, "A": A, "e": (255, 255, 255, 255), "p": (30, 20, 15, 255), "u": (255, 225, 120, 255), "o": shade(a, 1.3)}
grid = [
    "   o    o   ",
    "  aaaauaaa  ",
    " aaaaaaaaaa ",
    " aaeeaaeeaa ",
    " Aappaappa  ",
] + BODY_LOWER
save(grid, palette, "monstre-role-invocateur.png")

# Berserker (rouge vif, 0.85/0.18/0.18) : tête hérissée, yeux luminescents furieux.
a, A = role_palette(v4_to_rgb(0.85, 0.18, 0.18))
palette = {"a": a, "A": A, "r": (255, 210, 60, 255), "s": shade(a, 1.3)}
grid = [
    " s  s  s  s ",
    "  aaaaaaaa  ",
    " aarraarraa ",
    " aAaaaaaaAa ",
    " AAaaaaaaAA ",
] + BODY_LOWER
save(grid, palette, "monstre-role-berserker.png")

# Contrôleur (bleu, 0.35/0.6/0.85) : troisième œil, motif de chaîne sur le corps.
a, A = role_palette(v4_to_rgb(0.35, 0.60, 0.85))
palette = {"a": a, "A": A, "e": (255, 255, 255, 255), "p": (30, 20, 15, 255), "c": shade(a, 1.4)}
grid = [
    "     e      ",
    "   aaaaaa   ",
    "  aaeeaeea  ",
    "  aappaappa ",
    "  Aaccaccaa ",
] + BODY_LOWER
save(grid, palette, "monstre-role-controleur.png")

# ===========================================================================
# PNJ — redo du Villageois (couleurs exactes de Client/World/WorldMap.cs) + 3 PNJ nommés
# supplémentaires déjà présents en jeu (Garde royal, Marchande, Forgeron).
# ===========================================================================
def npc_grid(body, head, hair=(90, 70, 45, 255)):
    palette = {"s": head, "h": hair, "c": body, "C": shade(body, 0.72), "e": (40, 40, 45, 255)}
    return [
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
    ], palette


# Villageois — Npc("Villageois", ..., body 0.45/0.38/0.25, head 0.88/0.72/0.58).
grid, palette = npc_grid(body=v4_to_rgb(0.45, 0.38, 0.25), head=v4_to_rgb(0.88, 0.72, 0.58))
save(grid, palette, "pnj-villageois.png")

# Garde royal — Npc("Garde royal", ..., body 0.55/0.10/0.10, head 0.85/0.70/0.55).
grid, palette = npc_grid(body=v4_to_rgb(0.55, 0.10, 0.10), head=v4_to_rgb(0.85, 0.70, 0.55), hair=(40, 30, 25, 255))
save(grid, palette, "pnj-garde-royal.png")

# Marchande — Npc("Marchande", ..., body 0.20/0.45/0.35, head 0.90/0.75/0.60).
grid, palette = npc_grid(body=v4_to_rgb(0.20, 0.45, 0.35), head=v4_to_rgb(0.90, 0.75, 0.60))
save(grid, palette, "pnj-marchande.png")

# Forgeron — Npc("Forgeron", ..., body 0.30/0.30/0.32, head 0.80/0.62/0.48).
grid, palette = npc_grid(body=v4_to_rgb(0.30, 0.30, 0.32), head=v4_to_rgb(0.80, 0.62, 0.48), hair=(50, 40, 35, 255))
save(grid, palette, "pnj-forgeron.png")

print("OK - images generees dans", OUT_DIR)
for f in sorted(os.listdir(OUT_DIR)):
    if f.endswith(".png"):
        print(" -", f)
