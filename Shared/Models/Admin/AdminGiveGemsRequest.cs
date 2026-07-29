namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/game/give-gems</c> — équivalent HTTP de la commande de tchat /givegems (réservée au Fondateur), pour le panel F2.</summary>
public sealed class AdminGiveGemsRequest
{
    public required string SessionToken { get; init; }
    public required string TargetCharacterName { get; init; }
    public required long Amount { get; init; }
}
