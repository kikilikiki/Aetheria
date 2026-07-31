namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/game/give-monster</c> — voir GDD/demande utilisateur "les admin et le fonda peuvent aussi donner 1 monstre à un joueur".</summary>
public sealed class AdminGiveMonsterRequest
{
    public required string SessionToken { get; init; }
    public required string TargetCharacterName { get; init; }
    public required int SpeciesId { get; init; }
}
