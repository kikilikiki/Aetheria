using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>Voir GDD/demande utilisateur — "Système d'échange (trade) entre joueurs" : vue affichable côté Client (noms résolus, pas de Guid brut), aussi bien pour les offres reçues qu'envoyées.</summary>
public sealed class TradeOfferSummary
{
    public Guid Id { get; init; }
    public required string InitiatorName { get; init; }
    public required string TargetName { get; init; }
    public string? OfferedMonsterName { get; init; }
    public long OfferedGold { get; init; }
    public long RequestedGold { get; init; }
    public TradeOfferStatus Status { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
