namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/game/ban</c> — voir GDD/demande utilisateur, panel admin en jeu ("kick/ban/transformer").</summary>
public sealed class AdminBanRequest
{
    public required string SessionToken { get; init; }
    public required string TargetCharacterName { get; init; }
    public string? Reason { get; init; }
}
