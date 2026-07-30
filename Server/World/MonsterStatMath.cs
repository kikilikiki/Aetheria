using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;

namespace Aetheria.Server.World;

/// <summary>
/// Mise à l'échelle des statistiques de base d'une créature par niveau puis par variante (voir
/// MonsterVariantCatalog) — extrait de <c>CombatService</c> pour être réutilisé tel quel par
/// <see cref="WorldBossService"/> (voir GDD/demande utilisateur — "boss monde").
/// </summary>
public static class MonsterStatMath
{
    public static int ScaledStat(int baseStat, int level) => Math.Max(1, baseStat + (level - 1) * Math.Max(1, baseStat / 10));

    public static int ScaledStat(int baseStat, int level, MonsterVariant variant) =>
        Math.Max(1, (int)Math.Round(ScaledStat(baseStat, level) * MonsterVariantCatalog.Get(variant).StatMultiplier));

    /// <summary>Voir GDD/demande utilisateur — "Prestige après niveau maximum" : +5% permanent par palier de prestige, cumulé par-dessus le bonus de variante.</summary>
    public static int ScaledStat(int baseStat, int level, MonsterVariant variant, int prestigeLevel) =>
        Math.Max(1, (int)Math.Round(ScaledStat(baseStat, level, variant) * (1.0 + prestigeLevel * 0.05)));
}
