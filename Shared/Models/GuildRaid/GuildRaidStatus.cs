using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models.GuildRaid;

/// <summary>État courant du raid de guilde (voir GDD/demande utilisateur — "Raids de guilde"), ou <c>null</c> si aucun raid n'est actif pour cette guilde.</summary>
public sealed record GuildRaidStatus(
    Guid Id,
    string Name,
    int CurrentHealth,
    int MaxHealth,
    bool IsAlive,
    DateTime SpawnedAtUtc,
    DateTime? KilledAtUtc,
    string? KillerCharacterName,
    Element BossElement);
