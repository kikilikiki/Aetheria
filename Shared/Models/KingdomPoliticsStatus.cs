namespace Aetheria.Shared.Models;

/// <summary>Voir GDD/demande utilisateur — "Fonctionnalités de royaume avancées" (élections du roi, taxes, construction).</summary>
public sealed class KingdomPoliticsStatus
{
    public required int KingdomId { get; init; }
    public required string KingdomName { get; init; }
    public Guid? KingCharacterId { get; init; }
    public string? KingCharacterName { get; init; }
    public required long TreasuryGold { get; init; }
    public required int BonusTerritoryCount { get; init; }

    /// <summary>Voir GDD/demande utilisateur — "taxes (exemption avec premium palier 3)".</summary>
    public required bool IsTaxExempt { get; init; }
}
