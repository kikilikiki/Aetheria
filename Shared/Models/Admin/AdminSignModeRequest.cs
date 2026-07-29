namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/game/sign-mode</c> — voir GDD/demande utilisateur, "transformer le skin de tout les joueurs en panneau [...] pendant 5min".</summary>
public sealed class AdminSignModeRequest
{
    public required string SessionToken { get; init; }
    public int DurationSeconds { get; init; } = 300;
}
