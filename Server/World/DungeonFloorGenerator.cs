using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;

namespace Aetheria.Server.World;

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
            // L'étage jalon est entièrement dédié au combat de boss — une seule salle, pas de
            // disposition spatiale nécessaire (voir GDD — mini-boss/10, boss/50, boss légendaire/100).
            return new DungeonFloor(floorNumber, [new DungeonRoom(0, bossEncounter, IsStart: true)]);
        }

        var random = new Random(StableSeed(dungeonSeed, floorNumber));
        var positions = WalkGrid(random, RoomsPerFloor);

        var rooms = new List<DungeonRoom>(RoomsPerFloor);
        for (var i = 0; i < positions.Count; i++)
        {
            var (x, y) = positions[i];
            var isStart = i == 0;
            var encounter = isStart ? DungeonEncounterType.Evenement : PickEncounter(random);

            rooms.Add(new DungeonRoom(
                i, encounter, x, y,
                North: positions.Contains((x, y - 1)),
                South: positions.Contains((x, y + 1)),
                East: positions.Contains((x + 1, y)),
                West: positions.Contains((x - 1, y)),
                IsStart: isStart));
        }

        return new DungeonFloor(floorNumber, rooms);
    }

    /// <summary>
    /// Voir GDD/demande utilisateur — "des salles aléatoires... où on se déplace nous-même de
    /// salle en salle" : marche aléatoire depuis (0,0) façon Binding of Isaac — à chaque étape,
    /// tente une direction aléatoire depuis une salle déjà placée ; si la case cible est libre,
    /// une nouvelle salle y est créée. Le résultat est toujours connexe (chaque salle est
    /// atteignable depuis la salle de départ) puisqu'on ne part que de salles déjà posées.
    /// </summary>
    private static List<(int X, int Y)> WalkGrid(Random random, int roomCount)
    {
        var positions = new List<(int X, int Y)> { (0, 0) };
        var directions = new (int Dx, int Dy)[] { (0, -1), (0, 1), (1, 0), (-1, 0) };
        var safety = 0;

        while (positions.Count < roomCount && safety++ < roomCount * 50)
        {
            var from = positions[random.Next(positions.Count)];
            var (dx, dy) = directions[random.Next(directions.Length)];
            var next = (X: from.X + dx, Y: from.Y + dy);

            if (!positions.Contains(next))
            {
                positions.Add(next);
            }
        }

        return positions;
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

    /// <summary>
    /// Combine des entiers en une graine stable d'un lancement à l'autre du serveur.
    /// <see cref="HashCode.Combine{T1, T2}"/> est délibérément randomisé par processus
    /// (protection anti-collision) et NE DOIT PAS servir à une graine de génération procédurale
    /// censée être reproductible — piège rencontré et corrigé pendant le développement (voir
    /// <c>Docs/README.md</c>).
    /// </summary>
    public static int StableSeed(params ReadOnlySpan<int> values)
    {
        unchecked
        {
            var hash = 17;
            foreach (var value in values)
            {
                hash = (hash * 31) + value;
            }

            return hash;
        }
    }
}
