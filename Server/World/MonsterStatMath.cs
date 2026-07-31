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

    /// <summary>
    /// Voir GDD/demande utilisateur — "ajoute des iv comme sur pokémon", "ajoute aussi des ev" et
    /// "après un prestige ajoute un nouveau champ que on va appelé prest". Uniquement pour les
    /// créatures possédées (jamais les monstres sauvages/boss, qui n'ont pas ces colonnes) :
    /// IV et EV s'ajoutent en flat AVANT la mise à l'échelle par niveau/variante/prestige (donc
    /// ils en profitent aussi, comme une vraie composante de la statistique de base) ; EV est
    /// divisé par 4 comme dans Pokémon (252 EV -> +63 points avant mise à l'échelle). Prest
    /// s'ajoute APRÈS, en flat pur (un bonus de prestige ne doit pas se remultiplier par le
    /// prestige suivant).
    /// </summary>
    public static int ScaledStat(int baseStat, int level, MonsterVariant variant, int prestigeLevel, int iv, int ev, int prest) =>
        Math.Max(1, ScaledStat(baseStat + iv + ev / 4, level, variant, prestigeLevel) + prest);

    /// <summary>Voir GDD/demande utilisateur — "Talents/capacités passives uniques par monstre (comme les 'natures' Pokémon, influençant les stats)" : multiplicateur de nature appliqué sur la statistique de base, avant IV/EV.</summary>
    public static int ScaledStat(int baseStat, int level, MonsterVariant variant, int prestigeLevel, int iv, int ev, int prest, MonsterNature nature, MonsterStatKind statKind) =>
        ScaledStat((int)Math.Round(baseStat * MonsterNatureCatalog.Multiplier(nature, statKind)), level, variant, prestigeLevel, iv, ev, prest);
}
