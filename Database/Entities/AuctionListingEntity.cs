namespace Aetheria.Database.Entities;

/// <summary>
/// Une annonce sur l'Hôtel des ventes (voir GDD/demande utilisateur — "un HDV où les joueurs
/// mettent en vente et achètent, moins cher que chez la marchande [...] c'est nous qui mettons le
/// prix, avec la possibilité de le mettre aux enchères") : un joueur dépose des objets à un prix
/// de son choix, soit en achat immédiat (<see cref="PricePerUnit"/>), soit en véritable enchère
/// montante (<see cref="IsAuction"/>) résolue à l'expiration (voir AuctionService.ResolveExpiredAuctionsAsync,
/// appelé paresseusement à chaque consultation plutôt que par un scheduler dédié).
/// </summary>
public sealed class AuctionListingEntity
{
    public Guid Id { get; set; }
    public Guid SellerCharacterId { get; set; }
    public CharacterEntity? SellerCharacter { get; set; }

    public int ItemId { get; set; }
    public ItemEntity? Item { get; set; }

    public int Quantity { get; set; }
    public long PricePerUnit { get; set; }
    public DateTime ListedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Voir GDD/demande utilisateur — "la possibilité de le mettre aux enchères".</summary>
    public bool IsAuction { get; set; }

    /// <summary>Enchère courante (initialisée à <see cref="PricePerUnit"/> × <see cref="Quantity"/> au dépôt), sans effet si <see cref="IsAuction"/> est faux.</summary>
    public long CurrentBid { get; set; }

    public Guid? CurrentBidderCharacterId { get; set; }
    public string? CurrentBidderName { get; set; }
    public DateTime? AuctionEndsAtUtc { get; set; }
}
