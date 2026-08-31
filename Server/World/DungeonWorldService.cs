using Aetheria.Database.Entities;

namespace Aetheria.Server.World;

/// <summary>
/// Position dynamique des donjons sur la carte du monde (voir <c>Docs/GameDesign.md</c> —
/// "les donjons n'ont pas toujours un emplacement fixe. Ils apparaissent aléatoirement sur la
/// carte. Chaque heure, certains donjons disparaissent, de nouveaux apparaissent, leur position
/// change"). Recalculée paresseusement (à la lecture) plutôt que par une tâche planifiée : pas
/// de service en arrière-plan à faire tourner, et le résultat est strictement le même pour tous
/// les joueurs qui consultent la carte pendant la même heure UTC (seed déterministe).
///
/// Voir demande utilisateur — "toujours un donjon de niveau 1 et un donjon d'un niveau
/// aléatoire" : seuls 2 donjons (+1 éventuel invoqué par un admin) sont "actifs" à la fois, tirés
/// du catalogue et changés toutes les heures — voir <see cref="GetActivePortals"/>.
/// </summary>
public static class DungeonWorldService
{
    /// <summary>Taille de la carte du monde — doit rester cohérente avec <c>Client/World/WorldMap.cs</c>.</summary>
    public const int WorldSize = 50;

    public static long CurrentHourBucket => DateTime.UtcNow.Ticks / TimeSpan.FromHours(1).Ticks;

    public sealed record ActivePortal(DungeonEntity Dungeon, int Slot, int WorldX, int WorldY, bool IsAdminSpawned);

    /// <summary>
    /// Si la position enregistrée date d'une heure UTC révolue, en tire une nouvelle (déterministe
    /// pour cette heure). Retourne <c>true</c> si la position a changé (à sauvegarder par l'appelant).
    /// </summary>
    public static bool EnsureCurrentPosition(DungeonEntity dungeon)
    {
        var currentHourBucket = CurrentHourBucket;
        if (dungeon.PositionHourBucket == currentHourBucket)
        {
            return false;
        }

        var (x, y) = RollPosition(dungeon.Seed, currentHourBucket, slotOffset: 0);
        dungeon.WorldX = x;
        dungeon.WorldY = y;
        dungeon.PositionHourBucket = currentHourBucket;

        return true;
    }

    /// <summary>
    /// Les 2 (ou 3) donjons actifs pour l'heure demandée : slot 1 = un donjon de niveau minimum 1,
    /// slot 2 = un donjon tiré au sort parmi le reste, slot 3 = le donjon éventuellement forcé par
    /// un admin (voir <see cref="DungeonAdminOverride"/>). Tirage déterministe par heure UTC.
    /// </summary>
    public static IReadOnlyList<ActivePortal> GetActivePortals(IReadOnlyList<DungeonEntity> all, long hourBucket)
    {
        var eligible = all.Where(d => !d.IsMythic).ToList();
        if (eligible.Count == 0)
        {
            return [];
        }

        var portals = new List<ActivePortal>(3);
        var used = new HashSet<int>();

        // Slot 1 : un donjon "débutant" (niveau des monstres 1 à l'étage 1).
        var beginners = eligible.Where(d => d.MinLevel <= 1).ToList();
        var slot1 = PickDeterministic(beginners.Count > 0 ? beginners : eligible, DungeonFloorGenerator.StableSeed(1, (int)hourBucket), used);
        if (slot1 is not null)
        {
            portals.Add(BuildPortal(slot1, slot: 1, hourBucket, isAdminSpawned: false));
        }

        // Slot 2 : un autre donjon, quel que soit son niveau.
        var slot2 = PickDeterministic(eligible, DungeonFloorGenerator.StableSeed(2, (int)hourBucket), used);
        if (slot2 is not null)
        {
            portals.Add(BuildPortal(slot2, slot: 2, hourBucket, isAdminSpawned: false));
        }

        // Slot 3 : donjon forcé par un admin (peut être n'importe lequel, mythique inclus).
        if (DungeonAdminOverride.ActiveDungeonId(hourBucket) is { } forcedId
            && all.FirstOrDefault(d => d.Id == forcedId) is { } forced
            && used.Add(forced.Id))
        {
            portals.Add(BuildPortal(forced, slot: 3, hourBucket, isAdminSpawned: true));
        }

        return portals;
    }

    private static ActivePortal BuildPortal(DungeonEntity dungeon, int slot, long hourBucket, bool isAdminSpawned)
    {
        var (x, y) = RollPosition(dungeon.Seed, hourBucket, slotOffset: slot);
        return new ActivePortal(dungeon, slot, x, y, isAdminSpawned);
    }

    private static DungeonEntity? PickDeterministic(IReadOnlyList<DungeonEntity> pool, int seed, HashSet<int> used)
    {
        var available = pool.Where(d => !used.Contains(d.Id)).ToList();
        if (available.Count == 0)
        {
            return null;
        }

        var picked = available[new Random(seed).Next(available.Count)];
        used.Add(picked.Id);
        return picked;
    }

    /// <summary>
    /// Position déterministe d'un portail pour une heure donnée. Le <paramref name="slotOffset"/>
    /// décale le tirage pour que deux portails de la même heure ne se retrouvent pas sur la même
    /// case. Marge de 2 cases par rapport au bord (voir IsWithinBounds côté Client).
    /// </summary>
    private static (int X, int Y) RollPosition(int dungeonSeed, long hourBucket, int slotOffset)
    {
        var random = new Random(DungeonFloorGenerator.StableSeed(dungeonSeed, (int)hourBucket, slotOffset * 7919));
        return (random.Next(2, WorldSize - 2), random.Next(2, WorldSize - 2));
    }
}
