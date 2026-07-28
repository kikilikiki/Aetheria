namespace Aetheria.Database.Entities;

/// <summary>
/// Catalogue des donjons (table <c>Dungeons</c>) — chaque donjon représente une région entière
/// d'un royaume (voir <c>Docs/GameDesign.md</c> — section Donjons). <see cref="Seed"/> pilote la
/// génération procédurale déterministe des étages (voir <c>Server/World/DungeonFloorGenerator</c>).
/// </summary>
public sealed class DungeonEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int KingdomId { get; set; }
    public KingdomEntity? Kingdom { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Seed { get; set; }

    // Position dynamique sur la carte du monde (voir GDD — "les donjons n'ont pas toujours un
    // emplacement fixe", rotation toutes les heures). PositionHourBucket identifie l'heure UTC
    // (tronquée) pour laquelle WorldX/WorldY ont été calculés — voir DungeonWorldService.
    public int WorldX { get; set; }
    public int WorldY { get; set; }
    public long PositionHourBucket { get; set; } = -1;
}
