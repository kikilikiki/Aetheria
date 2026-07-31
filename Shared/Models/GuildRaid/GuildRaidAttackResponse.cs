namespace Aetheria.Shared.Models.GuildRaid;

/// <summary>Résultat d'une attaque contre le raid de guilde.</summary>
public sealed record GuildRaidAttackResponse(
    bool Success,
    string Message,
    int DamageDealt,
    long TotalDamageDealtByCharacter,
    bool BossKilled,
    int BossRemainingHealth);
