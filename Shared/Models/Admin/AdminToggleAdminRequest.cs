namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/game/toggle-admin</c> — voir GDD/demande utilisateur, bouton exclusif au Fondateur dans le panel admin en jeu.</summary>
public sealed class AdminToggleAdminRequest
{
    public required string SessionToken { get; init; }
    public required string TargetCharacterName { get; init; }
}
