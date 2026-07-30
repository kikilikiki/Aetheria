namespace Aetheria.Shared.Models;

/// <summary>Voir GDD/demande utilisateur — "indicateurs visuels quand double XP/loot sont actifs" : état public des minuteurs globaux (voir Server/World/GlobalEventService), interrogé par le Client pour afficher un badge tant qu'actif.</summary>
public sealed record GlobalEventStatus(bool IsDoubleXpActive, DateTime? DoubleXpUntilUtc, bool IsDoubleLootActive, DateTime? DoubleLootUntilUtc);
