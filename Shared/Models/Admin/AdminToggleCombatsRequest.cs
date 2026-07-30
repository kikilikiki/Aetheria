namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/game/toggle-combats</c> — voir retour utilisateur "ajouter un admin pour desactiver les combats".</summary>
public sealed class AdminToggleCombatsRequest
{
    public required string SessionToken { get; init; }
}
