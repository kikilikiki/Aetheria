using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models.Combat;

/// <summary>
/// Corps JSON de <c>POST /api/dungeons/{dungeonId}/floors/{floorNumber}/rooms/{roomIndex}/engage</c> :
/// engage le combat contre le monstre d'une salle générée procéduralement.
/// </summary>
public sealed class StartDungeonCombatRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required IReadOnlyList<Guid> MonsterIds { get; init; }

    /// <summary>
    /// Voir demande utilisateur — "quand on rentre ajoute le choix entre hardcore, normal ou
    /// spécial saison" : choisi une fois à l'entrée du donjon, porté jusqu'au combat engagé.
    /// Remplace l'ancien booléen <c>HardcoreRequested</c>.
    /// </summary>
    public DungeonModifier Modifier { get; init; } = DungeonModifier.Normal;
}
