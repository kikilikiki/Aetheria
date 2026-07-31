namespace Aetheria.Shared.Models.GuildRaid;

/// <summary>Corps JSON de <c>POST /api/guildraid/attack</c>.</summary>
public sealed class GuildRaidAttackRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
}
