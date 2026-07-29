namespace Aetheria.Database.Entities;

/// <summary>
/// Une annonce sur l'Hôtel des ventes (voir GDD/demande utilisateur — "un HDV où les joueurs
/// mettent en vente et achètent, moins cher que chez la marchande") : un joueur dépose des objets
/// à un prix de son choix, un autre les achète directement (pas d'enchères au sens strict malgré
/// le nom, pas de délai d'expiration pour cette première version).
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
}
