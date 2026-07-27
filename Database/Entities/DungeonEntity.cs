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
}
