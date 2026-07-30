namespace Aetheria.Shared.Models.WorldBoss;

/// <summary>État courant du boss mondial (voir GDD/demande utilisateur — "il a une barre de vie et peut etre tue"), ou <c>null</c> si aucun boss n'est actif.</summary>
public sealed record WorldBossStatus(
    Guid Id,
    string Name,
    int CurrentHealth,
    int MaxHealth,
    bool IsAlive,
    DateTime SpawnedAtUtc,
    DateTime? KilledAtUtc,
    string? KillerCharacterName);
