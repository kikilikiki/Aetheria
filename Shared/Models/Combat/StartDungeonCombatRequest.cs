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

    /// <summary>Voir GDD/demande utilisateur — "fait en sorte que les dongon hardcore soit pas des dongon a part mais que l'on peut choisir hardcore ou normal" : n'a d'effet que si le donjon propose le mode hardcore (voir DungeonEntity.IsHardcore).</summary>
    public bool HardcoreRequested { get; init; }
}
