using Aetheria.Shared.Enums;

namespace Aetheria.Database.Entities;

/// <summary>
/// Voir GDD/demande utilisateur — "Système d'échange (trade) entre joueurs" : offre d'échange
/// ciblée (contrairement à l'Hôtel des ventes, voir AuctionService, qui est un marché anonyme) —
/// un joueur propose sa créature (optionnelle) plus de l'or, contre de l'or demandé et/ou une
/// créature précise du joueur ciblé (voir Docs/Idees.md — <see cref="RequestedMonsterId"/>,
/// jusqu'ici la contrepartie ne pouvait être qu'en or). Reste en
/// <see cref="TradeOfferStatus.Pending"/> jusqu'à acceptation/refus/annulation (voir TradeService).
/// </summary>
public sealed class TradeOfferEntity
{
    public Guid Id { get; set; }
    public Guid InitiatorCharacterId { get; set; }
    public Guid TargetCharacterId { get; set; }
    public Guid? OfferedMonsterId { get; set; }
    public long OfferedGold { get; set; }
    public long RequestedGold { get; set; }

    /// <summary>Voir Docs/Idees.md — créature du joueur CIBLÉ demandée en contrepartie (distincte de <see cref="OfferedMonsterId"/>, qui appartient à l'initiateur). Validée comme appartenant toujours à la cible à la fois à la proposition et à l'acceptation (voir TradeService).</summary>
    public Guid? RequestedMonsterId { get; set; }
    public TradeOfferStatus Status { get; set; } = TradeOfferStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
