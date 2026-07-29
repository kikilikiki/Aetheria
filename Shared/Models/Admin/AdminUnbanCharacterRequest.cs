namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/game/unban</c> — équivalent HTTP de la commande de tchat /unban, pour le panel F2 (débannissement rapide sans quitter le jeu).</summary>
public sealed class AdminUnbanCharacterRequest
{
    public required string SessionToken { get; init; }
    public required string TargetCharacterName { get; init; }
}
