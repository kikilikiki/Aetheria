namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/game/transform</c> — voir GDD/demande utilisateur, "transformer en panneau" ciblé sur un seul joueur.</summary>
public sealed class AdminTransformRequest
{
    public required string SessionToken { get; init; }
    public required string TargetCharacterName { get; init; }
    public int DurationSeconds { get; init; } = 60;
}
