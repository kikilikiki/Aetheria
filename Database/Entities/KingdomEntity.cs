using Aetheria.Shared.Enums;

namespace Aetheria.Database.Entities;

/// <summary>Royaume (table <c>Kingdoms</c>) : un par valeur de <see cref="KingdomType"/>.</summary>
public sealed class KingdomEntity
{
    public int Id { get; set; }
    public KingdomType Type { get; set; }
    public required string Name { get; set; }
    public string CapitalName { get; set; } = string.Empty;

    /// <summary>Points de guerre accumulés depuis la dernière résolution hebdomadaire (voir Server/World/KingdomWarService.cs).</summary>
    public long WarPoints { get; set; }

    /// <summary>
    /// Voir GDD/demande utilisateur — "le 1er gagne 2 batiments, le 2nd gagne 1, le 3ieme ne gagne
    /// et ne perd rien, le 4ieme perd 1 batiment" : plutôt que de faire apparaître/disparaître des
    /// bâtiments à des coordonnées aléatoires sur la carte (nécessiterait une carte du monde
    /// pilotée par le serveur — voir Docs/README.md), ce compteur augmente/diminue le rendement de
    /// récolte de TOUS les territoires contrôlés par ce royaume (voir ProfessionService.GatherAsync).
    /// </summary>
    public int BonusTerritoryCount { get; set; }
}
