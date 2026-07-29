using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Économie premium (voir GDD/demande utilisateur — "shop avec des gems, si les personnes
/// payent avec de l'argent réel on peut obtenir des gems"). Deux paliers indépendants et
/// cumulatifs, achetés en gemmes (<see cref="UserEntity.Gems"/>) :
/// - "Grade" (<see cref="UserEntity.PremiumGradeTier"/>) : cosmétique + petit boost XP/or.
/// - "Pass d'emplacement de personnage" (<see cref="UserEntity.CharacterSlotTier"/>) : augmente
///   le nombre maximum de personnages (2 de base).
/// Le Fondateur obtient toujours l'équivalent du palier maximum des deux sans dépenser de
/// gemmes (voir GDD — "le grade fondateur peut les avoir sans payer, pas de limite de
/// personnage"). Aucune vraie passerelle de paiement n'est branchée pour le moment (voir GDD,
/// "bloque la page pour le moment") : les gemmes ne sont créditées que manuellement (voir
/// <c>PlayerSession</c> — <c>/givegems</c>, réservé au Fondateur) ou par conversion de pièces
/// (voir <see cref="GoldPerGemBlock"/>).
/// </summary>
public static class PremiumService
{
    public const int MaxTier = 3;

    /// <summary>Nombre maximum de personnages par palier (indice = <see cref="UserEntity.CharacterSlotTier"/>) — 2 de base, +1, +1, +2 (voir GDD).</summary>
    private static readonly int[] SlotTierMaxCharacters = [2, 3, 4, 6];

    /// <summary>Coût en gemmes pour ATTEINDRE ce palier depuis le précédent (indice 0 inutilisé, palier de départ).</summary>
    private static readonly long[] SlotTierCostGems = [0, 5, 10, 20];

    /// <summary>Bonus XP/or en pourcentage par palier de grade (voir GDD — "0.1% le 1er, 0.2% le second, 0.3% le troisième").</summary>
    private static readonly double[] GradeTierBonusPercent = [0, 0.1, 0.2, 0.3];

    private static readonly long[] GradeTierCostGems = [0, 5, 10, 20];

    /// <summary>Voir GDD/demande utilisateur — "transformer 100 millions de coins en 10 gems".</summary>
    public const long GoldPerGemBlock = 100_000_000;
    public const long GemsPerGemBlock = 10;

    public static int MaxCharacters(UserEntity user) =>
        user.Rank == UserRank.Fondateur ? int.MaxValue : SlotTierMaxCharacters[Math.Clamp(user.CharacterSlotTier, 0, MaxTier)];

    public static double XpGoldMultiplier(UserEntity user) =>
        1.0 + GradeTierBonusPercent[EffectiveGradeTier(user)] / 100.0;

    private static int EffectiveGradeTier(UserEntity user) =>
        user.Rank == UserRank.Fondateur ? MaxTier : Math.Clamp(user.PremiumGradeTier, 0, MaxTier);

    /// <summary>Coût en gemmes pour passer au palier de grade suivant, ou <c>null</c> si déjà au maximum.</summary>
    public static long? NextGradeTierCost(UserEntity user) =>
        user.PremiumGradeTier >= MaxTier ? null : GradeTierCostGems[user.PremiumGradeTier + 1];

    /// <summary>Coût en gemmes pour le prochain pass d'emplacement de personnage, ou <c>null</c> si déjà au maximum.</summary>
    public static long? NextSlotTierCost(UserEntity user) =>
        user.CharacterSlotTier >= MaxTier ? null : SlotTierCostGems[user.CharacterSlotTier + 1];

    public static async Task<double> GetXpGoldMultiplierAsync(AetheriaDbContext db, Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user is null ? 1.0 : XpGoldMultiplier(user);
    }

    public static Aetheria.Shared.Models.Premium.PremiumStatus ToStatus(UserEntity user) => new()
    {
        Gems = user.Gems,
        GradeTier = user.Rank == UserRank.Fondateur ? MaxTier : user.PremiumGradeTier,
        GradeBonusPercent = GradeTierBonusPercent[EffectiveGradeTier(user)],
        NextGradeTierCostGems = user.Rank == UserRank.Fondateur ? null : NextGradeTierCost(user),
        CharacterSlotTier = user.CharacterSlotTier,
        MaxCharacters = MaxCharacters(user),
        NextCharacterSlotCostGems = user.Rank == UserRank.Fondateur ? null : NextSlotTierCost(user),
        GoldPerGemBlock = GoldPerGemBlock,
        GemsPerGemBlock = GemsPerGemBlock,
    };
}
