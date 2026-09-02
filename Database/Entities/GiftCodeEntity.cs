namespace Aetheria.Database.Entities;

/// <summary>
/// Code cadeau à saisir sur le site ou dans le Launcher pour obtenir une récompense (voir demande
/// utilisateur). <b>Aucune récompense n'est encore distribuée</b> : la rédemption est enregistrée
/// (<see cref="GiftCodeRedemptionEntity"/>) et <see cref="RewardPayload"/> décrit ce qui sera
/// accordé plus tard, quand le système de récompenses sera branché.
/// </summary>
public sealed class GiftCodeEntity
{
    public Guid Id { get; set; }

    /// <summary>Le code saisi par le joueur (comparé sans tenir compte de la casse — stocké en majuscules).</summary>
    public required string Code { get; set; }

    /// <summary>Description lisible de la récompense (affichée au joueur après rédemption).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Contenu machine de la récompense (JSON, à interpréter par le futur service de récompenses). Vide pour l'instant.</summary>
    public string RewardPayload { get; set; } = string.Empty;

    /// <summary>Nombre maximum de rédemptions au total (null = illimité).</summary>
    public int? MaxRedemptions { get; set; }

    public int RedemptionCount { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? ExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
