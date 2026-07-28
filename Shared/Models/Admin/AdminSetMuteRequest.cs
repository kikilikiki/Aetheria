namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/users/{id}/set-mute</c> — voir GDD/demande utilisateur, "mute pour ne pas qu'il parle dans le tchat".</summary>
public sealed class AdminSetMuteRequest
{
    public required string SessionToken { get; init; }
    public required bool IsMuted { get; init; }
}
