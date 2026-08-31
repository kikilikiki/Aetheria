using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Économie premium (voir GDD/demande utilisateur — "shop avec des gems" puis "Grades Aetheria
/// (achetés en Gems) : Aventurier/Héros/Légende"). Un seul palier cumulatif, acheté en gemmes
/// (<see cref="UserEntity.Gems"/>, <see cref="UserEntity.PremiumGradeTier"/>), qui combine
/// désormais bonus XP/or, emplacements de personnage et badge cosmétique — les paliers séparés
/// "grade" et "pass de personnage" d'une version précédente ont été fusionnés en un seul système
/// pour coller exactement à la grille de prix fournie. Le Fondateur obtient toujours l'équivalent
/// du palier maximum sans dépenser de gemmes (voir GDD — "le grade fondateur peut les avoir sans
/// payer, pas de limite de personnage"). Aucune vraie passerelle de paiement n'est branchée pour
/// le moment (voir GDD, "bloque la page pour le moment") : les gemmes ne sont créditées que
/// manuellement (voir <c>PlayerSession</c> — <c>/givegems</c>, réservé au Fondateur) ou par
/// conversion de pièces (voir <see cref="GoldPerGemBlock"/>).
///
/// Perks NON implémentés, faute de système existant pour les porter (voir rapport donné à
/// l'utilisateur) : emote exclusive, coffre premium mensuel, monture cosmétique exclusive, skins
/// légendaires, "file d'attente boutique améliorée". Implémentés : bonus XP/or, emplacements de
/// personnage, badge + couleur de pseudo dans le tchat (voir PlayerSession, ChatMessagePacket).
/// </summary>
public static class PremiumService
{
    public const int MaxTier = 3;

    public static readonly string[] GradeTierNames = ["Aucun", "Aventurier", "Héros", "Légende"];

    /// <summary>Emplacements de personnage EN PLUS des 2 de base, par palier (voir GDD — "+1/+2/+5 emplacements").</summary>
    private static readonly int[] GradeTierSlotBonus = [0, 1, 2, 5];

    /// <summary>Bonus XP/or en pourcentage par palier (voir GDD — "0.1%/0.2%/0.3% bonus xp / money").</summary>
    private static readonly double[] GradeTierBonusPercent = [0, 0.1, 0.2, 0.3];

    /// <summary>Coût en gemmes pour ATTEINDRE ce palier (voir GDD — "500/1500/3000 Gems").</summary>
    private static readonly long[] GradeTierCostGems = [0, 500, 1500, 3000];

    /// <summary>Voir GDD/demande utilisateur — "transformer 100 millions de coins en 10 gems".</summary>
    public const long GoldPerGemBlock = 100_000_000;
    public const long GemsPerGemBlock = 10;

    /// <summary>
    /// Voir demande utilisateur — "achat en gemmes de X2 XP global puis on repaie pour X4 puis X8
    /// etc de plus en plus cher" : coût en gemmes du prochain palier, à partir du multiplicateur
    /// mondial en cours. Base 250 gemmes (×1 → ×2), puis ×3 par palier (×2 → ×4 : 750, ×4 → ×8 :
    /// 2250, ×8 → ×16 : 6750…).
    /// </summary>
    public const long GlobalXpBoostBaseCostGems = 250;

    public static long GemCostForNextGlobalXpTier(double currentMultiplier)
    {
        var tier = (int)Math.Round(Math.Log2(Math.Max(1.0, currentMultiplier)));
        return GlobalXpBoostBaseCostGems * (long)Math.Pow(3, tier);
    }

    public static int MaxCharacters(UserEntity user) =>
        user.Rank == UserRank.Fondateur ? int.MaxValue : 2 + GradeTierSlotBonus[EffectiveGradeTier(user)];

    public static double XpGoldMultiplier(UserEntity user) =>
        1.0 + GradeTierBonusPercent[EffectiveGradeTier(user)] / 100.0;

    private static int EffectiveGradeTier(UserEntity user) =>
        user.Rank == UserRank.Fondateur ? MaxTier : Math.Clamp(user.PremiumGradeTier, 0, MaxTier);

    /// <summary>Coût en gemmes pour passer au palier de grade suivant, ou <c>null</c> si déjà au maximum.</summary>
    public static long? NextGradeTierCost(UserEntity user) =>
        user.PremiumGradeTier >= MaxTier ? null : GradeTierCostGems[user.PremiumGradeTier + 1];

    public static async Task<double> GetXpGoldMultiplierAsync(AetheriaDbContext db, Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user is null ? 1.0 : XpGoldMultiplier(user);
    }

    /// <summary>Voir GDD/demande utilisateur — badge affiché devant le pseudo dans le tchat, en plus du grade de modération (voir PlayerSession, ChatRankTag côté client).</summary>
    public static string? BadgeTag(UserEntity user) =>
        EffectiveGradeTier(user) switch
        {
            1 => "[AVENTURIER]",
            2 => "[HEROS]",
            3 => "[LEGENDE]",
            _ => null,
        };

    public static Aetheria.Shared.Models.Premium.PremiumStatus ToStatus(UserEntity user) => new()
    {
        Gems = user.Gems,
        GradeTier = EffectiveGradeTier(user),
        GradeName = GradeTierNames[EffectiveGradeTier(user)],
        GradeBonusPercent = GradeTierBonusPercent[EffectiveGradeTier(user)],
        NextGradeTierCostGems = user.Rank == UserRank.Fondateur ? null : NextGradeTierCost(user),
        NextGradeTierName = user.PremiumGradeTier >= MaxTier || user.Rank == UserRank.Fondateur ? null : GradeTierNames[user.PremiumGradeTier + 1],
        MaxCharacters = MaxCharacters(user),
        GoldPerGemBlock = GoldPerGemBlock,
        GemsPerGemBlock = GemsPerGemBlock,
    };
}
