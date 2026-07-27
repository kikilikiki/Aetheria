namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/guilds/{guildId}/join</c>.</summary>
public sealed class JoinGuildRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
}
