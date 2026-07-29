namespace Aetheria.Shared.Models.Premium;

/// <summary>
/// État de l'économie premium d'un compte (voir GDD/demande utilisateur — "Grades Aetheria
/// (achetés en Gems) : Aventurier/Héros/Légende") : gemmes possédées, grade (bonus XP/or +
/// emplacements de personnage + badge).
/// </summary>
public sealed class PremiumStatus
{
    public required long Gems { get; init; }

    public required int GradeTier { get; init; }
    public required string GradeName { get; init; }
    public required double GradeBonusPercent { get; init; }
    public long? NextGradeTierCostGems { get; init; }
    public string? NextGradeTierName { get; init; }

    public required int MaxCharacters { get; init; }

    /// <summary>Voir GDD/demande utilisateur — "transformer 100 millions de coins en 10 gems".</summary>
    public required long GoldPerGemBlock { get; init; }
    public required long GemsPerGemBlock { get; init; }

    /// <summary>Voir GDD/demande utilisateur — "bloque la page pour le moment" : aucune passerelle de paiement réel n'est branchée, le client doit afficher l'achat de gemmes contre argent réel comme indisponible.</summary>
    public bool RealMoneyPurchaseAvailable => false;
}
