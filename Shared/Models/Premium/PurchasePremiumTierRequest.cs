namespace Aetheria.Shared.Models.Premium;

/// <summary>Achète le palier de grade ou de pass de personnage suivant (voir GDD/demande utilisateur — "shop avec des gems") — un seul palier à la fois, dans l'ordre.</summary>
public sealed class PurchasePremiumTierRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
}
