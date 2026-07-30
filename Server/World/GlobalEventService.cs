using Aetheria.Shared.Enums;

namespace Aetheria.Server.World;

/// <summary>
/// Voir GDD/demande utilisateur — "commandes admin abuse : double XP, double butin, invasion de
/// monstres" : minuteurs partagés par TOUS les joueurs (connectés ou non), indépendants et
/// multiplicatifs avec les boosts individuels (voir <see cref="TemporaryBoostService"/>,
/// <c>/globalboost</c>) plutôt que de les remplacer. En mémoire uniquement (repart à zéro au
/// redémarrage du serveur), comme les autres états globaux éphémères de cette échelle (voir
/// <c>CombatSessionStore</c>, <c>ArenaQueueService</c>).
/// </summary>
public static class GlobalEventService
{
    private static DateTime? _doubleXpUntilUtc;
    private static DateTime? _doubleLootUntilUtc;
    private static readonly Dictionary<KingdomType, DateTime> InvasionUntilUtcByKingdom = new();

    /// <summary>Voir GDD/demande utilisateur — "ajouter un admin pour desactiver les combats" : bascule manuelle (pas de minuterie), voir CombatService — bloque le lancement de tout nouveau combat (PvE, donjon, duel) tant qu'actif.</summary>
    private static bool _combatsDisabled;

    public static void ActivateDoubleXp(TimeSpan duration) => _doubleXpUntilUtc = DateTime.UtcNow + duration;
    public static void ActivateDoubleLoot(TimeSpan duration) => _doubleLootUntilUtc = DateTime.UtcNow + duration;
    public static void ActivateInvasion(KingdomType kingdom, TimeSpan duration) => InvasionUntilUtcByKingdom[kingdom] = DateTime.UtcNow + duration;
    public static void SetCombatsDisabled(bool disabled) => _combatsDisabled = disabled;
    public static bool AreCombatsDisabled => _combatsDisabled;

    public static double XpMultiplier => _doubleXpUntilUtc > DateTime.UtcNow ? 2.0 : 1.0;
    public static int LootMultiplier => _doubleLootUntilUtc > DateTime.UtcNow ? 2 : 1;

    public static bool IsInvasionActive(KingdomType kingdom) =>
        InvasionUntilUtcByKingdom.TryGetValue(kingdom, out var until) && until > DateTime.UtcNow;

    /// <summary>Voir GDD/demande utilisateur — "invasion de monstres" : pendant une invasion, les rencontres sauvages du royaume ciblé tirent uniquement des variantes dangereuses (Alpha/Corrompu) au lieu de la pondération normale.</summary>
    public static MonsterVariant RollInvasionVariant(Random random) => random.Next(2) == 0 ? MonsterVariant.Alpha : MonsterVariant.Corrompu;
}
