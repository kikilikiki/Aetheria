namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/game/set-level</c> — équivalent HTTP de la commande de tchat /setlevel, pour le panel F2.</summary>
public sealed class AdminSetLevelRequest
{
    public required string SessionToken { get; init; }
    public required string TargetCharacterName { get; init; }
    public required int Level { get; init; }
}
