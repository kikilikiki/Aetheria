# Concepts visuels (générés par code)

Voir `Docs/Idees.md` — "Sprites/textures réels" : intégrer de vraies textures dans le moteur a
été volontairement mis de côté (aucun asset graphique réel disponible, remplacer les silhouettes
vectorielles par des textures tout aussi géométriques n'aurait rien apporté). À la place, ce
dossier contient des **mockups pixel-art très simples, générés par code** (`generate_concepts.py`,
Python + Pillow) pour donner une idée de direction artistique possible — pas du vrai art, juste
des blocs de couleur pour visualiser à quoi une vraie sprite pourrait ressembler une fois
dessinée par un·e artiste.

Les couleurs reprennent exactement celles déjà utilisées dans le code (`Client/World/WorldMap.cs`
pour les PNJ nommés, `Client/World/CharacterAppearancePalette.cs` pour le joueur,
`CombatTypeColor` dans `Client/Program.cs` pour les rôles de monstre) pour rester cohérentes avec
ce qui est déjà affiché en jeu.

**Mise à jour du 2026-08-27** : deuxième passe demandée pour couvrir tout le roster — les
personnages/monstres d'origine ont été redessinés et complétés par un exemple pour chacun des
10 rôles de monstre (`Shared/Enums/MonsterType.cs`), 3 variantes de personnalisation du joueur, et
les PNJ/bâtiments nommés déjà présents dans le monde. `personnage-joueur-guerrier.png` et
`monstre-archetype-tank.png` ont été supprimés au profit des fichiers listés ci-dessous
(renommés pour rester cohérents avec le reste du set).

## Fichiers

### Personnage joueur

Le moteur n'a pas de "classe" de personnage (seuls les monstres ont un rôle de combat) — ces 3
fichiers montrent l'étendue de la personnalisation déjà existante (peau/cheveux/vêtements/
accessoire, voir `CharacterAppearancePalette.cs`), pas 3 classes différentes.

- `personnage-joueur-a.png` — peau Hâlée, cheveux Roux, vêtements Bleu.
- `personnage-joueur-b.png` — peau Foncée, cheveux Blanc, vêtements Violet, accessoire Couronne.
- `personnage-joueur-c.png` — peau Verte, cheveux Vert, vêtements Noir, accessoire Bandeau.

### Monstres — un exemple par rôle de combat

Couleur de base = `CombatTypeColor` (la couleur déjà utilisée en combat pour ce rôle), silhouette
distincte par accessoire (oreilles/cornes/ailes/halo/cornes...) pour rester lisible en petit
format tout en gardant un air de famille (une seule "espèce" générique par rôle, pas un vrai
bestiaire par royaume/élément).

- `monstre-role-guerrier.png`, `monstre-role-archer.png`, `monstre-role-soigneur.png`,
  `monstre-role-tank.png`, `monstre-role-mage.png`, `monstre-role-assassin.png`,
  `monstre-role-support.png`, `monstre-role-invocateur.png`, `monstre-role-berserker.png`,
  `monstre-role-controleur.png`.
- `monstre-braisillon.png` — starter Feu par élément plutôt que par rôle (voir
  `MonsterCatalogSeeder` : "petite salamandre qui couve des braises sous ses écailles"), gardé
  comme exemple de bestiaire "par élément" en plus des rôles ci-dessus.

### PNJ nommés (voir `WorldMap.cs`, couleurs `Npc(...)` reprises telles quelles)

- `pnj-villageois.png`, `pnj-garde-royal.png`, `pnj-marchande.png`, `pnj-forgeron.png`.

### Bâtiments visitables (voir `BuildingInterior.cs` pour les intérieurs correspondants)

- `batiment-capitale.png`, `batiment-forge.png`, `batiment-auberge.png`,
  `batiment-hotel-des-ventes.png`, `batiment-guilde.png`.

## Régénérer

```bash
python3 Docs/Image/generate_concepts.py
```

Nécessite Pillow (`pip install Pillow`). Le script régénère tous les fichiers PNG ci-dessus dans
ce même dossier — modifiable directement (grilles de caractères → couleurs) pour essayer
d'autres formes/palettes sans outil de dessin.
