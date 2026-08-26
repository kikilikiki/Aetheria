# Concepts visuels (générés par code)

Voir `Docs/Idees.md` — "Sprites/textures réels" : intégrer de vraies textures dans le moteur a
été volontairement mis de côté (aucun asset graphique réel disponible, remplacer les silhouettes
vectorielles par des textures tout aussi géométriques n'aurait rien apporté). À la place, ce
dossier contient des **mockups pixel-art très simples, générés par code** (`generate_concepts.py`,
Python + Pillow) pour donner une idée de direction artistique possible — pas du vrai art, juste
des blocs de couleur pour visualiser à quoi une vraie sprite pourrait ressembler une fois
dessinée par un·e artiste.

Les palettes reprennent les couleurs déjà utilisées dans le code (`Client/World/WorldMap.cs`,
`KingdomBiome.cs`, `Server/Persistence/MonsterCatalogSeeder.cs`) pour rester cohérentes avec ce
qui est déjà affiché en jeu.

## Fichiers

- `personnage-joueur-guerrier.png` — personnage joueur, silhouette Guerrier neutre.
- `batiment-capitale.png` — bâtiment Capitale (teinte or, voir `WorldMap.cs`).
- `monstre-braisillon.png` — starter Feu (voir `MonsterCatalogSeeder` : "petite salamandre qui
  couve des braises sous ses écailles").
- `monstre-archetype-tank.png` — silhouette pour l'archétype de rôle Tank (teintes pierre, voir
  Golem Rocheux/Yéti/... dans le bestiaire étendu).
- `pnj-villageois.png` — PNJ générique de ville.

## Régénérer

```bash
python3 Docs/Image/generate_concepts.py
```

Nécessite Pillow (`pip install Pillow`). Le script régénère tous les fichiers PNG ci-dessus dans
ce même dossier — modifiable directement (grilles de caractères → couleurs) pour essayer
d'autres formes/palettes sans outil de dessin.
