using Aetheria.Shared.Enums;

namespace Aetheria.Database.Entities;

/// <summary>
/// Voir GDD/demande utilisateur — "Système d'échange (trade) entre joueurs" : offre d'échange
/// ciblée (contrairement à l'Hôtel des ventes, voir AuctionService, qui est un marché anonyme) —
/// un joueur propose sa créature (optionnelle) plus de l'or, contre de l'or demandé à un joueur
/// précis nommément désigné. Reste en <see cref="TradeOfferStatus.Pending"/> jusqu'à acceptation/
/// refus/annulation (voir TradeService).
/// </summary>
public sealed class TradeOfferEntity
{
    public Guid Id { get; set; }
    public Guid InitiatorCharacterId { get; set; }
    public Guid TargetCharacterId { get; set; }
    public Guid? OfferedMonsterId { get; set; }
    public long OfferedGold { get; set; }
    public long RequestedGold { get; set; }
    public TradeOfferStatus Status { get; set; } = TradeOfferStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
