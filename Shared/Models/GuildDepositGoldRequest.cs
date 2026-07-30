namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/guilds/{id}/deposit-gold</c> — voir GDD/demande utilisateur "Banque de guilde" et "Niveau de guilde".</summary>
public sealed class GuildDepositGoldRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required long Amount { get; init; }
}
