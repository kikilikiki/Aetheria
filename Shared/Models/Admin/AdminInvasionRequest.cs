using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/game/invasion</c> — voir GDD/demande utilisateur "commandes admin abuse : invasion de monstres".</summary>
public sealed class AdminInvasionRequest
{
    public required string SessionToken { get; init; }
    public required KingdomType Kingdom { get; init; }
    public int DurationMinutes { get; init; } = 30;
}
