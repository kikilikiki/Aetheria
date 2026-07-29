namespace Aetheria.Shared.Enums;

/// <summary>Type de territoire qu'un royaume peut contrôler (voir <c>Docs/GameDesign.md</c> — section Royaumes).</summary>
public enum TerritoryType
{
    Mine,
    Village,
    Fort,
    Donjon,

    /// <summary>Voir GDD/demande utilisateur — "guerre de territoire... des bâtiments (mine, champs etc)" : capturable comme la Mine.</summary>
    Champ,
}
