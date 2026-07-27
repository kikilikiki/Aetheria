using Aetheria.Shared.Enums;

namespace Aetheria.Server.World;

/// <summary>Une salle d'étage de donjon et son contenu.</summary>
public sealed record DungeonRoom(int Index, DungeonEncounterType EncounterType);

/// <summary>Le contenu généré d'un étage : ses salles, dans l'ordre de traversée.</summary>
public sealed record DungeonFloor(int FloorNumber, IReadOnlyList<DungeonRoom> Rooms);

/// <summary>
/// Génère le contenu d'un étage de donjon de façon déterministe à partir de
/// <c>(dungeonSeed, floorNumber)</c> : le même étage produit toujours le même contenu, mais
/// deux donjons différents (seeds différents) divergent (voir <c>Docs/GameDesign.md</c> —
/// section Donjons : mini-boss tous les 10 étages, boss tous les 50, boss légendaire tous les
/// 100). La disposition spatiale des salles (grille, corridors) reste à faire côté
/// Client/MapEditor ; ce générateur ne produit que la séquence de rencontres.
/// </summary>
public static class DungeonFloorGenerator
{
    private const int RoomsPerFloor = 6;

    private static readonly (DungeonEncounterType Type, int Weight)[] EncounterWeights =
    [
        (DungeonEncounterType.Monstre, 40),
        (DungeonEncounterType.Evenement, 10),
        (DungeonEncounterType.Enigme, 8),
        (DungeonEncounterType.Coffre, 15),
        (DungeonEncounterType.Piege, 12),
        (DungeonEncounterType.Marchand, 5),
        (DungeonEncounterType.SalleSecrete, 5),
        (DungeonEncounterType.Autel, 5),
    ];

    public static DungeonFloor GenerateFloor(int dungeonSeed, int floorNumber)
    {
        if (floorNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(floorNumber), "Le numéro d'étage doit être positif.");
        }

        var milestone = GetMilestoneEncounter(floorNumber);
        if (milestone is { } bossEncounter)
        {
            // L'étage jalon est entièrement dédié au combat de boss.
            return new DungeonFloor(floorNumber, [new DungeonRoom(0, bossEncounter)]);
        }

        var random = new Random(HashCode.Combine(dungeonSeed, floorNumber));
        var rooms = new List<DungeonRoom>(RoomsPerFloor);
        for (var i = 0; i < RoomsPerFloor; i++)
        {
            rooms.Add(new DungeonRoom(i, PickEncounter(random)));
        }

        return new DungeonFloor(floorNumber, rooms);
    }

    private static DungeonEncounterType? GetMilestoneEncounter(int floorNumber)
    {
        if (floorNumber % 100 == 0)
        {
            return DungeonEncounterType.BossLegendaire;
        }

        if (floorNumber % 50 == 0)
        {
            return DungeonEncounterType.Boss;
        }

        if (floorNumber % 10 == 0)
        {
            return DungeonEncounterType.MiniBoss;
        }

        return null;
    }

    private static DungeonEncounterType PickEncounter(Random random)
    {
        var totalWeight = EncounterWeights.Sum(e => e.Weight);
        var roll = random.Next(totalWeight);
        var cumulative = 0;

        foreach (var (type, weight) in EncounterWeights)
        {
            cumulative += weight;
            if (roll < cumulative)
            {
                return type;
            }
        }

        return DungeonEncounterType.Monstre;
    }
}
