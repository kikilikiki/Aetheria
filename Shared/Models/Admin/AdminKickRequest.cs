namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/game/kick</c> — voir GDD/demande utilisateur, panel admin en jeu "kick".</summary>
public sealed class AdminKickRequest
{
    public required string SessionToken { get; init; }
    public required string TargetCharacterName { get; init; }
}
