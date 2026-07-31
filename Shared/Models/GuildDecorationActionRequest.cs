namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/guilds/{id}/decorations/purchase</c> et <c>/set-active</c> — voir GDD/demande utilisateur "Housing/décoration de guilde ou de royaume".</summary>
public sealed class GuildDecorationActionRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required string DecorationKey { get; init; }
}
