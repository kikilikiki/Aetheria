namespace Aetheria.Shared.World;

/// <summary>
/// Voir GDD/demande utilisateur — "en combat on peut encore traverser les mur" : jusqu'ici aucune
/// collision n'existait nulle part (ni client, ni serveur), en combat comme hors combat — le
/// combat tactique sur grille (<c>CombatEngine</c>) est un système à part, sans rapport avec les
/// bâtiments de ville. Emplacements des bâtiments de la capitale (voir <c>Client/World/WorldMap.cs</c>,
/// dont l'arithmétique est reprise ici telle quelle plutôt que dupliquée) partagés entre Client et
/// Serveur pour qu'un déplacement ne puisse jamais être accepté sur une case occupée par un
/// bâtiment, des deux côtés à la fois (validation autoritaire côté serveur, prédiction identique
/// côté client).
/// </summary>
public static class TownLayout
{
    /// <summary>Taille de carte utilisée par le Client pour toute capitale (voir <c>RebuildWorldMapForKingdom</c>) — pas encore configurable.</summary>
    public const int DefaultSize = 50;

    /// <summary>Rayon d'emprise au sol autour de chaque bâtiment (case du bâtiment + les 4 voisines orthogonales) — approximation volontairement simple, voir <c>Client/World/WorldMap.cs</c> ("pas de vraie emprise au sol, silhouettes stylisées").</summary>
    private const int FootprintRadius = 1;

    public static IReadOnlyList<(int X, int Y)> BuildingCells(int size)
    {
        var capital = (X: size / 2, Y: size / 2);
        var village = (X: size / 2 - 10, Y: size / 2 - 8);
        var auctionHouse = (X: size / 2 + 8, Y: size / 2 - 6);
        var forge = (X: size / 2 - 6, Y: size / 2 + 9);
        var guild = (X: size / 2 + 9, Y: size / 2 + 8);
        var teleporter = (X: size / 2 - 3, Y: size / 2 - 4);
        var pension = (X: size / 2 + 3, Y: size / 2 - 4);
        var mine = (X: size / 2 - 8, Y: size / 2 + 3);
        var shop = (X: auctionHouse.X + 3, Y: auctionHouse.Y + 2);
        var field = (X: village.X - 4, Y: village.Y + 5);
        var warRoom = (X: guild.X + 3, Y: guild.Y - 3);
        var fusion = (X: size / 2 - 1, Y: size / 2 + 6);
        var hatchery = (X: size / 2 + 6, Y: size / 2 + 2);

        return [capital, village, auctionHouse, forge, guild, teleporter, pension, mine, shop, field, warRoom, fusion, hatchery];
    }

    public static bool IsWalkable(int x, int y, int size)
    {
        if (x < 0 || x >= size || y < 0 || y >= size)
        {
            return false;
        }

        foreach (var (buildingX, buildingY) in BuildingCells(size))
        {
            var dx = Math.Abs(x - buildingX);
            var dy = Math.Abs(y - buildingY);
            if (dx + dy <= FootprintRadius)
            {
                return false;
            }
        }

        return true;
    }
}
