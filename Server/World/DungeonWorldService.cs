using Aetheria.Database.Entities;

namespace Aetheria.Server.World;

/// <summary>
/// Position dynamique des donjons sur la carte du monde (voir <c>Docs/GameDesign.md</c> —
/// "les donjons n'ont pas toujours un emplacement fixe. Ils apparaissent aléatoirement sur la
/// carte. Chaque heure, certains donjons disparaissent, de nouveaux apparaissent, leur position
/// change"). Recalculée paresseusement (à la lecture) plutôt que par une tâche planifiée : pas
/// de service en arrière-plan à faire tourner, et le résultat est strictement le même pour tous
/// les joueurs qui consultent la carte pendant la même heure UTC (seed déterministe).
/// </summary>
public static class DungeonWorldService
{
    /// <summary>Taille de la carte du monde — doit rester cohérente avec <c>Client/World/WorldMap.cs</c>.</summary>
    public const int WorldSize = 50;

    /// <summary>
    /// Si la position enregistrée date d'une heure UTC révolue, en tire une nouvelle (déterministe
    /// pour cette heure). Retourne <c>true</c> si la position a changé (à sauvegarder par l'appelant).
    /// </summary>
    public static bool EnsureCurrentPosition(DungeonEntity dungeon)
    {
        var currentHourBucket = DateTime.UtcNow.Ticks / TimeSpan.FromHours(1).Ticks;
        if (dungeon.PositionHourBucket == currentHourBucket)
        {
            return false;
        }

        var seed = DungeonFloorGenerator.StableSeed(dungeon.Seed, (int)currentHourBucket);
        var random = new Random(seed);

        // Marge de 2 cases par rapport au bord pour ne jamais placer un donjon hors carte
        // (voir IsWithinBounds côté Client) ni collé au bord visuel.
        dungeon.WorldX = random.Next(2, WorldSize - 2);
        dungeon.WorldY = random.Next(2, WorldSize - 2);
        dungeon.PositionHourBucket = currentHourBucket;

        return true;
    }
}
