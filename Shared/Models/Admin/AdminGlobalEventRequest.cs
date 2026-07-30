namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/game/double-xp</c> et <c>/double-loot</c> — voir GDD/demande utilisateur "commandes admin abuse : double XP, double butin".</summary>
public sealed class AdminGlobalEventRequest
{
    public required string SessionToken { get; init; }
    public int DurationMinutes { get; init; } = 30;
}
