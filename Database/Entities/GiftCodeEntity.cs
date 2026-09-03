using Aetheria.Shared.Enums;

namespace Aetheria.Database.Entities;

/// <summary>
/// Code cadeau à saisir sur le site ou dans le Launcher pour obtenir une récompense (voir demande
/// utilisateur — "on choisit de donner quoi : des gems, de l'argent, des monstres et autre,
/// réservé au Fondateur"). Les récompenses configurées ci-dessous sont créditées à la rédemption
/// (voir <see cref="GiftCodeRedemptionEntity"/> et <c>GiftCodeRedeemer</c>).
/// </summary>
public sealed class GiftCodeEntity
{
    public Guid Id { get; set; }

    /// <summary>Le code saisi par le joueur (comparé sans tenir compte de la casse — stocké en majuscules).</summary>
    public required string Code { get; set; }

    /// <summary>Description lisible de la récompense (affichée au joueur après rédemption). Sert aussi de "et autre" (texte libre) quand la récompense n'est pas modélisable ci-dessous.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gemmes créditées au compte (account-wide, <see cref="UserEntity.Gems"/>). 0 = aucune.</summary>
    public long RewardGems { get; set; }

    /// <summary>Or crédité au personnage qui utilise le code (<see cref="CharacterEntity.Gold"/>). 0 = aucun. Nécessite un contexte personnage (rédemption depuis le Launcher, ou choix d'un personnage sur le site).</summary>
    public long RewardGold { get; set; }

    /// <summary>Espèce de créature offerte (id de <see cref="MonsterSpeciesEntity"/>) ; null = aucune. Comme l'or, nécessite un contexte personnage.</summary>
    public int? RewardMonsterSpeciesId { get; set; }

    /// <summary>Niveau de la créature offerte (1..150).</summary>
    public int RewardMonsterLevel { get; set; } = 1;

    /// <summary>Variante de la créature offerte (Normal par défaut).</summary>
    public MonsterVariant RewardMonsterVariant { get; set; } = MonsterVariant.Normal;

    /// <summary>Contenu machine libre (JSON) pour une récompense non modélisée — inutilisé par défaut.</summary>
    public string RewardPayload { get; set; } = string.Empty;

    /// <summary>Nombre maximum de rédemptions au total (null = illimité).</summary>
    public int? MaxRedemptions { get; set; }

    public int RedemptionCount { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? ExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
