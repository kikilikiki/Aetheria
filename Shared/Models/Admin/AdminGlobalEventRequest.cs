namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/game/double-xp</c>, <c>/double-loot</c> et <c>/rare-boost</c> — voir GDD/demande utilisateur "commandes admin abuse".</summary>
public sealed class AdminGlobalEventRequest
{
    public required string SessionToken { get; init; }
    public int DurationMinutes { get; init; } = 30;

    /// <summary>Voir demande utilisateur — "l'admin choisit le multiplicateur au moment de le lancer" (XP globale, boost variantes/capture).</summary>
    public double Multiplier { get; init; } = 2.0;
}
