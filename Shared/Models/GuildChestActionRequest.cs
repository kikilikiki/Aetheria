namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/guilds/{id}/chest/deposit</c> et <c>/withdraw</c> — voir GDD/demande utilisateur "Coffre partagé".</summary>
public sealed class GuildChestActionRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required int ItemId { get; init; }
    public required int Quantity { get; init; }
}
