namespace Aetheria.Shared.Models.WorldBoss;

/// <summary>Corps JSON de <c>POST /api/admin/game/spawn-world-boss</c> — voir GDD/demande utilisateur "boss geant mondial", réservé aux comptes admin/fondateur.</summary>
public sealed class SpawnWorldBossRequest
{
    public required string SessionToken { get; init; }
    public required string Name { get; init; }
    public required int MaxHealth { get; init; }
}
