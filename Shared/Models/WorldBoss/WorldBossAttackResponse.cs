namespace Aetheria.Shared.Models.WorldBoss;

/// <summary>Résultat d'une attaque contre le boss mondial.</summary>
public sealed record WorldBossAttackResponse(
    bool Success,
    string Message,
    int DamageDealt,
    long TotalDamageDealtByCharacter,
    bool BossKilled,
    int BossRemainingHealth);
