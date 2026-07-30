namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/guilds/{guildId}/join</c>.</summary>
public sealed class JoinGuildRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }

    /// <summary>Voir GDD/demande utilisateur — "guildes privees (peut join avec code 5 chiffres)" : requis (et doit correspondre) si la guilde ciblée n'est pas publique, ignoré sinon.</summary>
    public string? JoinCode { get; init; }
}
