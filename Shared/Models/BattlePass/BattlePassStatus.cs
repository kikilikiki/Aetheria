namespace Aetheria.Shared.Models.BattlePass;

/// <summary>
/// État du Passe de Niveau d'un personnage (voir GDD/demande utilisateur — "un pass de niveaux de
/// joueur ou chaque xp que tu gagne est ajouté dedans aussi ou chaque passage te fait gagner
/// quelque chose"). Voir <c>Server/World/BattlePassService.cs</c> pour la logique.
/// </summary>
public sealed class BattlePassStatus
{
    public required int Level { get; init; }
    public required long Experience { get; init; }
    public required long ExperienceForNextLevel { get; init; }
    public required bool HasPremium { get; init; }

    /// <summary>Coût en gemmes pour débloquer le pass premium, ou <c>null</c> s'il est déjà actif.</summary>
    public long? PremiumCostGems { get; init; }

    /// <summary>Dernier palier offrant une récompense catalogué (au-delà, le niveau continue de monter sans nouvelle récompense).</summary>
    public required int MaxRewardLevel { get; init; }

    /// <summary>Voir GDD/demande utilisateur — "une route que l'on peut scroll" : un palier par niveau de 1 à <see cref="MaxRewardLevel"/>.</summary>
    public required IReadOnlyList<BattlePassTier> Tiers { get; init; }
}
