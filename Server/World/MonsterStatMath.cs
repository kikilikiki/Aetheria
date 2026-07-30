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
}
