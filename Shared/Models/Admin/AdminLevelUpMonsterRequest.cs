namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/game/level-up-monster</c> — voir GDD/demande utilisateur, "ajoute au admin la possibilité d'augmenter le niveau de ces monstres".</summary>
public sealed class AdminLevelUpMonsterRequest
{
    public required string SessionToken { get; init; }
    public required Guid MonsterId { get; init; }
    public int Levels { get; init; } = 1;
}
