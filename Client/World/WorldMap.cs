using System.Numerics;
using Aetheria.Shared.Enums;
using Aetheria.Shared.World;

namespace Aetheria.Client.World;

/// <summary>
/// Génère la capitale du royaume du personnage : terrain varié (herbe, chemins, étang), une
/// capitale et ses bâtiments, et l'entrée d'un donjon de test — voir <see cref="KingdomBiome"/>
/// pour la palette/les noms propres à chaque royaume (voir GDD — "plusieurs villes distinctes
/// par royaume/biome"). Calculé une seule fois au chargement, pas à chaque frame.
/// </summary>
/// <summary>Voir Docs/Idees.md — rencontre sauvage pondérée par terrain : les 3 variantes d'herbe (jusqu'ici purement cosmétiques, voir <see cref="WorldMap.ComputeTileColor"/>) ont désormais un sens de gameplay via <see cref="WorldMap.GetTerrain"/>.</summary>
public enum TerrainType
{
    Path,
    Water,
    GrassLight,
    GrassMid,
    GrassDark,
}

public sealed class WorldMap
{
    public int Size { get; }
    public Vector4[,] TileColors { get; }
    private readonly TerrainType[,] _terrain;
    public IReadOnlyList<Building> Buildings { get; }
    public IReadOnlyList<Npc> Npcs { get; }
    public (int X, int Y) SpawnPosition { get; }
    public (int X, int Y) DungeonEntrance { get; private set; }
    public string DungeonName { get; }

    /// <summary>Identifiant serveur du donjon affiché ici, résolu après coup via <see cref="SetDungeon"/> (voir GET /api/dungeons) — -1 tant qu'inconnu.</summary>
    public int DungeonId { get; private set; } = -1;

    private readonly Vector4 GrassLight;
    private readonly Vector4 GrassMid;
    private readonly Vector4 GrassDark;
    private readonly Vector4 DirtPath;
    private readonly Vector4 WaterBlue;

    private static readonly Vector4 Gold = new(0.85f, 0.70f, 0.25f, 1f);
    private static readonly Vector4 DarkGold = new(0.55f, 0.44f, 0.14f, 1f);
    private static readonly Vector4 Tan = new(0.72f, 0.58f, 0.40f, 1f);
    private static readonly Vector4 Brown = new(0.45f, 0.34f, 0.22f, 1f);
    private static readonly Vector4 SteelBlue = new(0.40f, 0.55f, 0.68f, 1f);
    private static readonly Vector4 DarkBlue = new(0.22f, 0.32f, 0.45f, 1f);
    private static readonly Vector4 Ember = new(0.75f, 0.35f, 0.20f, 1f);
    private static readonly Vector4 DarkEmber = new(0.45f, 0.20f, 0.12f, 1f);
    private static readonly Vector4 Purple = new(0.55f, 0.35f, 0.65f, 1f);
    private static readonly Vector4 DarkPurple = new(0.32f, 0.20f, 0.40f, 1f);

    /// <summary>Couleurs du portail de donjon, de l'anneau extérieur (sombre) au cœur (pulsant, mélangé au moment du rendu).</summary>
    public static readonly Vector4 PortalOuterColor = new(0.05f, 0.02f, 0.08f, 1f);
    public static readonly Vector4 PortalMidColorDark = new(0.32f, 0.20f, 0.40f, 1f);
    public static readonly Vector4 PortalMidColorBright = new(0.68f, 0.38f, 0.88f, 1f);
    public static readonly Vector4 PortalCoreColor = new(0.88f, 0.70f, 0.98f, 1f);

    /// <summary>Couleur des enseignes (plaque en bois clair) posées devant chaque bâtiment.</summary>
    public static readonly Vector4 SignboardColor = new(0.82f, 0.72f, 0.55f, 1f);
    public static readonly Vector4 SignpostColor = new(0.35f, 0.25f, 0.16f, 1f);

    private readonly HashSet<(int X, int Y)> _pathTiles;
    private readonly (int X, int Y) _pond;

    public WorldMap(int size, KingdomType kingdom = KingdomType.Nature)
    {
        var biome = KingdomBiome.For(kingdom);
        DungeonName = biome.DungeonName;
        GrassLight = biome.GrassLight;
        GrassMid = biome.GrassMid;
        GrassDark = biome.GrassDark;
        DirtPath = biome.GroundPath;
        WaterBlue = biome.Water;
        var tint = biome.AccentTint;

        Size = size;
        TileColors = new Vector4[size, size];
        _terrain = new TerrainType[size, size];

        // Voir GDD/demande utilisateur — "en combat on peut encore traverser les mur" : cette
        // arithmétique de placement est désormais partagée avec le serveur (voir
        // Shared/World/TownLayout.cs, réutilisé tel quel ici) plutôt que dupliquée, pour que la
        // validation de collision serveur (PlayerSession.HandlePlayerMove) et l'affichage client
        // ne puissent jamais diverger.
        var buildingCells = TownLayout.BuildingCells(size);
        var capital = buildingCells[0];
        var village = buildingCells[1];
        var auctionHouse = buildingCells[2];
        var forge = buildingCells[3];
        var guild = buildingCells[4];
        var teleporter = buildingCells[5];
        var pension = buildingCells[6];
        var mine = buildingCells[7];
        var shop = buildingCells[8];
        var field = buildingCells[9];
        var warRoom = buildingCells[10];
        var fusion = buildingCells[11];
        var hatchery = buildingCells[12];

        SpawnPosition = (capital.X, capital.Y + 2);
        DungeonEntrance = (size - 4, size - 4);

        var pathTiles = new HashSet<(int X, int Y)>();
        foreach (var target in new[] { village, auctionHouse, forge, guild, teleporter, pension, mine, shop, field, warRoom, fusion, hatchery, DungeonEntrance })
        {
            MarkPath(pathTiles, capital, target);
        }

        _pathTiles = pathTiles;
        _pond = (X: 6, Y: size - 8);
        var pond = _pond;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var terrain = ComputeTerrain(x, y, pathTiles, pond);
                _terrain[x, y] = terrain;
                TileColors[x, y] = ColorForTerrain(terrain);
            }
        }

        // Bâtiments teintés selon le royaume (AccentTint, voir KingdomBiome) plutôt que des
        // couleurs entièrement redessinées par royaume — garde la même disposition/lisibilité
        // tout en rendant chaque capitale visuellement distincte.
        Buildings =
        [
            new Building(biome.CapitalName, capital.X, capital.Y, 2.6f, Gold * tint, DarkGold * tint, Gold * tint * 0.8f),
            new Building("Aubergiste", village.X, village.Y, 1.4f, Tan * tint, Brown * tint, Tan * tint * 0.8f),
            new Building("Hôtel des ventes", auctionHouse.X, auctionHouse.Y, 1.6f, SteelBlue * tint, DarkBlue * tint, SteelBlue * tint * 0.8f),
            new Building("Forge", forge.X, forge.Y, 1.5f, Ember * tint, DarkEmber * tint, Ember * tint * 0.8f),
            new Building("Guilde", guild.X, guild.Y, 1.8f, Purple * tint, DarkPurple * tint, Purple * tint * 0.8f),
            // Voir GDD/demande utilisateur — "ajoute un téléporteur pour se déplacer de ville en
            // ville mais notre team ne change pas" : violet/mystique plutôt que teinté par royaume,
            // pour rester visuellement identique dans toutes les villes (repère facile à trouver).
            new Building("Téléporteur", teleporter.X, teleporter.Y, 1.2f, new Vector4(0.55f, 0.35f, 0.85f, 1f), new Vector4(0.32f, 0.18f, 0.55f, 1f), new Vector4(0.7f, 0.5f, 0.95f, 1f)),
            // Voir GDD/demande utilisateur — "un bâtiment où l'on peut voir tout nos monstres et
            // déplacer ce que l'on a dans notre team" : ouvre directement le panneau Monstres
            // existant (touche M) plutôt qu'une scène d'intérieur dédiée.
            new Building("Pension", pension.X, pension.Y, 1.3f, new Vector4(0.35f, 0.65f, 0.45f, 1f), new Vector4(0.18f, 0.38f, 0.24f, 1f), new Vector4(0.5f, 0.8f, 0.6f, 1f)),
            // Voir GDD/demande utilisateur — "guerre de territoire... pour que les joueurs de sa
            // team puissent aller faire des quêtes de minage" : nom propre au royaume (voir
            // KingdomBiome.MineName, mêmes noms que Server/Persistence/TerritorySeeder.cs) —
            // peut appartenir à un autre royaume si celui-ci a gagné la dernière guerre.
            new Building(biome.MineName, mine.X, mine.Y, 1.4f, new Vector4(0.5f, 0.45f, 0.4f, 1f) * tint, new Vector4(0.3f, 0.26f, 0.22f, 1f) * tint, new Vector4(0.6f, 0.55f, 0.5f, 1f) * tint),
            // Voir GDD/demande utilisateur — "ajoute un bâtiment d'un marchand où on peut acheter/
            // vendre au lieu de l'UI Boutique en haut à droite" : bâtiment dédié à la Marchande
            // plutôt qu'un simple raccourci clavier/HUD.
            new Building("Boutique", shop.X, shop.Y, 1.3f, new Vector4(0.55f, 0.42f, 0.2f, 1f) * tint, new Vector4(0.35f, 0.25f, 0.1f, 1f) * tint, new Vector4(0.7f, 0.55f, 0.3f, 1f) * tint),
            // Voir GDD/demande utilisateur — "guerre de territoire... des bâtiments (mine, champs
            // etc)" : même mécanique de capture/contrôle que la Mine (nom propre au royaume, voir
            // KingdomBiome.FieldName, mêmes noms que TerritorySeeder.cs).
            new Building(biome.FieldName, field.X, field.Y, 1.2f, new Vector4(0.78f, 0.68f, 0.28f, 1f) * tint, new Vector4(0.5f, 0.42f, 0.16f, 1f) * tint, new Vector4(0.85f, 0.78f, 0.4f, 1f) * tint),
            // Voir GDD/demande utilisateur — "il y a un bâtiment nommé Guerre où on rentre dedans,
            // ça nous met une UI pour dire quand on est prêt" : rouge sang plutôt que teinté par
            // royaume (repère facile à trouver, comme le Téléporteur), pas de contrôle de
            // territoire pour ce bâtiment lui-même (c'est la porte d'entrée du matchmaking).
            new Building("Guerre", warRoom.X, warRoom.Y, 1.4f, new Vector4(0.62f, 0.18f, 0.16f, 1f), new Vector4(0.38f, 0.1f, 0.08f, 1f), new Vector4(0.78f, 0.3f, 0.26f, 1f)),
            // Voir GDD/demande utilisateur — "un batiment pour fusionner des monstres (leur niveau
            // sera leur 2 niveaux additionnes puis divise par 2)" : violet/rose fusion, distinct
            // des autres bâtiments.
            new Building("Fusion", fusion.X, fusion.Y, 1.3f, new Vector4(0.6f, 0.3f, 0.55f, 1f), new Vector4(0.36f, 0.16f, 0.32f, 1f), new Vector4(0.75f, 0.42f, 0.68f, 1f)),
            // Voir GDD/demande utilisateur — "un batiment pour faire de la reproduction avec
            // heritage de statistiques" : rose tendre, thème "couvée".
            new Building("Reproduction", hatchery.X, hatchery.Y, 1.3f, new Vector4(0.85f, 0.55f, 0.6f, 1f), new Vector4(0.55f, 0.3f, 0.35f, 1f), new Vector4(0.95f, 0.7f, 0.75f, 1f)),
        ];

        Npcs =
        [
            // Voir retour utilisateur — "rapprocher le garde" : était à 2 cases de la capitale
            // (le double de tous les autres PNJ, systématiquement à 1 case de leur bâtiment) -
            // ramené à la même échelle, plus proche du point d'arrivée du joueur.
            new Npc("Garde royal", capital.X - 1, capital.Y, new Vector4(0.55f, 0.10f, 0.10f, 1f), new Vector4(0.85f, 0.70f, 0.55f, 1f), 0f),
            new Npc("Marchande", auctionHouse.X + 1, auctionHouse.Y + 1, new Vector4(0.20f, 0.45f, 0.35f, 1f), new Vector4(0.90f, 0.75f, 0.60f, 1f), 1.3f),
            new Npc("Forgeron", forge.X + 1, forge.Y, new Vector4(0.30f, 0.30f, 0.32f, 1f), new Vector4(0.80f, 0.62f, 0.48f, 1f), 2.6f),
            new Npc("Villageois", village.X + 2, village.Y + 1, new Vector4(0.45f, 0.38f, 0.25f, 1f), new Vector4(0.88f, 0.72f, 0.58f, 1f), 4.0f),
        ];
    }

    /// <summary>
    /// Voir Docs/Idees.md — "vraie géographie pour les îles volantes/aquatiques" : au lieu d'un
    /// simple succès caché (voir <c>ExplorationService</c>, toujours vérifié côté serveur avant
    /// d'accepter la visite), une vraie petite carte explorable, réutilisant le même mécanisme que
    /// le voyage entre royaumes (voir <see cref="RebuildWorldMapForKingdom"/> côté Client) plutôt
    /// que d'ajouter une notion d'élévation/eau traversable au moteur. **Limite assumée** : réutilise
    /// le même espace de coordonnées (0..<paramref name="size"/>) que les capitales — la validation
    /// serveur de collision (<c>TownLayout.IsWalkable</c>) reste donc calée sur le plan de capitale
    /// standard plutôt que sur la disposition (vide) de l'île, cohérent avec la simplification déjà
    /// assumée ailleurs (un seul espace de coordonnées partagé entre les royaumes, voir
    /// GDD — visibilité globale).
    /// </summary>
    public WorldMap(int size, MountKind islandKind)
    {
        Size = size;
        TileColors = new Vector4[size, size];
        _terrain = new TerrainType[size, size];
        DungeonName = "";
        _pathTiles = [];
        _pond = (-100, -100); // hors carte : pas d'étang sur une île.

        var isSky = islandKind == MountKind.Volant;
        GrassLight = isSky ? new Vector4(0.85f, 0.90f, 0.98f, 1f) : new Vector4(0.86f, 0.78f, 0.55f, 1f);
        GrassMid = isSky ? new Vector4(0.75f, 0.82f, 0.95f, 1f) : new Vector4(0.80f, 0.70f, 0.45f, 1f);
        GrassDark = isSky ? new Vector4(0.65f, 0.74f, 0.92f, 1f) : new Vector4(0.72f, 0.60f, 0.36f, 1f);
        DirtPath = isSky ? new Vector4(0.55f, 0.62f, 0.85f, 1f) : new Vector4(0.65f, 0.52f, 0.30f, 1f);
        // Voir GDD/demande utilisateur — île aquatique : l'eau occupe tout l'espace en dehors du
        // petit plateau central où le joueur peut se tenir (voir ComputeTerrain, rayon centré).
        WaterBlue = new Vector4(0.20f, 0.35f, 0.60f, 1f);

        // Voir Docs/Idees.md — le point d'arrivée reprend exactement la même formule que
        // WorldMap.SpawnPosition pour une capitale (voir plus bas, capital.X/capital.Y + 2) :
        // reste ainsi accepté par la même exception serveur (CapitalSpawnPoint, voir
        // PlayerSession.HandlePlayerMove) sans changement côté serveur, cohérent avec le voyage
        // entre royaumes qui réutilise déjà ce même point d'arrivée quel que soit le royaume.
        var capital = TownLayout.BuildingCells(size)[0];
        var center = (X: capital.X, Y: capital.Y + 2);
        SpawnPosition = center;
        DungeonEntrance = (-100, -100);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                TerrainType terrain;
                if (!isSky)
                {
                    // Île aquatique : plateau de sable/herbe au centre, océan tout autour.
                    var distanceToCenter = MathF.Sqrt(MathF.Pow(x - center.X, 2) + MathF.Pow(y - center.Y, 2));
                    terrain = distanceToCenter < 9f ? ComputeTerrain(x, y, [], (-100, -100)) : TerrainType.Water;
                }
                else
                {
                    terrain = ComputeTerrain(x, y, [], (-100, -100));
                }

                _terrain[x, y] = terrain;
                TileColors[x, y] = ColorForTerrain(terrain);
            }
        }

        Buildings = [new Building("Retour", center.X, center.Y - 3, 1.2f,
            new Vector4(0.6f, 0.5f, 0.85f, 1f), new Vector4(0.35f, 0.28f, 0.55f, 1f), new Vector4(0.75f, 0.65f, 0.95f, 1f))];
        Npcs = [];
    }

    public bool IsWithinBounds(int x, int y) => x >= 0 && x < Size && y >= 0 && y < Size;

    /// <summary>
    /// Zone où des mobs sauvages peuvent surgir hors donjon (voir GDD) : herbe libre, ni chemin
    /// ni étang. **Limite assumée** : pas de vraie emprise au sol pour les bâtiments (silhouettes
    /// stylisées, voir <c>Docs/README.md</c>), donc pas exclus ici non plus.
    /// </summary>
    public bool IsWildEncounterZone(int x, int y) =>
        IsWithinBounds(x, y)
        && !_pathTiles.Contains((x, y))
        && MathF.Sqrt(MathF.Pow(x - _pond.X, 2) + MathF.Pow(y - _pond.Y, 2)) >= 4.5f;

    /// <summary>
    /// Applique la position serveur du donjon (voir <c>DungeonWorldService</c> côté serveur — la
    /// position tourne chaque heure UTC). Appelé une fois après connexion (voir Program.cs) : le
    /// donjon garde sa position d'origine tant que le client n'est pas encore connecté/n'a pas
    /// encore reçu la liste des donjons. **Limite assumée** : les chemins de terre tracés à la
    /// construction (<see cref="MarkPath"/>) ne sont pas retracés vers la nouvelle position —
    /// seule l'entrée (portail + zone d'interaction) se déplace réellement.
    /// </summary>
    public void SetDungeon(int id, int worldX, int worldY)
    {
        DungeonId = id;
        if (IsWithinBounds(worldX, worldY))
        {
            DungeonEntrance = (worldX, worldY);
        }
    }

    private static void MarkPath(HashSet<(int X, int Y)> pathTiles, (int X, int Y) from, (int X, int Y) to)
    {
        var x = from.X;
        var y = from.Y;

        while (x != to.X)
        {
            pathTiles.Add((x, y));
            x += Math.Sign(to.X - x);
        }

        while (y != to.Y)
        {
            pathTiles.Add((x, y));
            y += Math.Sign(to.Y - y);
        }

        pathTiles.Add((to.X, to.Y));
    }

    private static TerrainType ComputeTerrain(int x, int y, HashSet<(int X, int Y)> pathTiles, (int X, int Y) pond)
    {
        if (pathTiles.Contains((x, y)))
        {
            return TerrainType.Path;
        }

        var distanceToPond = MathF.Sqrt(MathF.Pow(x - pond.X, 2) + MathF.Pow(y - pond.Y, 2));
        if (distanceToPond < 4.5f)
        {
            return TerrainType.Water;
        }

        return Hash(x, y) switch
        {
            < 0.33f => TerrainType.GrassLight,
            < 0.66f => TerrainType.GrassMid,
            _ => TerrainType.GrassDark,
        };
    }

    private Vector4 ColorForTerrain(TerrainType terrain) => terrain switch
    {
        TerrainType.Path => DirtPath,
        TerrainType.Water => WaterBlue,
        TerrainType.GrassLight => GrassLight,
        TerrainType.GrassMid => GrassMid,
        TerrainType.GrassDark => GrassDark,
        _ => GrassMid,
    };

    /// <summary>Voir Docs/Idees.md — rencontre sauvage pondérée par terrain : <see cref="TerrainType.Path"/> hors limites par défaut (poids nul côté appelant, cohérent avec <see cref="IsWildEncounterZone"/>).</summary>
    public TerrainType GetTerrain(int x, int y) => IsWithinBounds(x, y) ? _terrain[x, y] : TerrainType.Path;

    /// <summary>Hash déterministe [0,1) par case, pour une variation de terrain reproductible sans dépendance externe.</summary>
    private static float Hash(int x, int y)
    {
        unchecked
        {
            var h = (x * 374761393) + (y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7fffffff) / (float)int.MaxValue;
        }
    }
}
