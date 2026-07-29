namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/game/give-money</c> — voir retour utilisateur, "il manque des commandes dans le panel admin (F2)" (équivalent HTTP de la commande de tchat /givemoney).</summary>
public sealed class AdminGiveMoneyRequest
{
    public required string SessionToken { get; init; }
    public required string TargetCharacterName { get; init; }
    public required long Amount { get; init; }
}
