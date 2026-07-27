using Aetheria.Shared.Enums;

namespace Aetheria.Database.Entities;

/// <summary>
/// Territoire (mine, village, fort, donjon) qu'un royaume peut contrôler (table <c>Territories</c>,
/// voir <c>Docs/GameDesign.md</c> — section Royaumes : "chaque semaine, les territoires peuvent
/// changer de propriétaire").
/// </summary>
public sealed class TerritoryEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public TerritoryType TerritoryType { get; set; }

    public int ControllingKingdomId { get; set; }
    public KingdomEntity? ControllingKingdom { get; set; }
}
