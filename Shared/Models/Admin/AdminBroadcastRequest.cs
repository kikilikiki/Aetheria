namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/game/broadcast</c> — voir GDD/demande utilisateur, "afficher un message en haut de l'écran en gros à tout les joueurs".</summary>
public sealed class AdminBroadcastRequest
{
    public required string SessionToken { get; init; }
    public required string Message { get; init; }
}
