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
    private static DateTime? _globalXpUntilUtc;
    private static double _globalXpMultiplier = 1.0;
    private static DateTime? _doubleLootUntilUtc;
    private static DateTime? _rareBoostUntilUtc;
    private static double _rareBoostMultiplier = 1.0;
    private static readonly Dictionary<KingdomType, DateTime> InvasionUntilUtcByKingdom = new();

    /// <summary>Voir GDD/demande utilisateur — "ajouter un admin pour desactiver les combats" : bascule manuelle (pas de minuterie), voir CombatService — bloque le lancement de tout nouveau combat (PvE, donjon, duel) tant qu'actif.</summary>
    private static bool _combatsDisabled;

    /// <summary>Voir demande utilisateur — "XP globale, l'admin choisit le multiplicateur" : XP ×N pour tout le monde pendant une durée donnée (remplace l'ancien XP ×2 fixe).</summary>
    public static void SetGlobalXp(double multiplier, TimeSpan duration)
    {
        _globalXpMultiplier = Math.Clamp(multiplier, 1.0, 64.0);
        _globalXpUntilUtc = DateTime.UtcNow + duration;
    }

    /// <summary>Voir demande utilisateur — "achat en gemmes : X2 puis on repaie pour X4 puis X8…" : double le multiplicateur d'XP mondial en cours (ou le pose à ×2 s'il est inactif) et prolonge le minuteur.</summary>
    public static double EscalateGlobalXp(TimeSpan duration)
    {
        _globalXpMultiplier = _globalXpUntilUtc > DateTime.UtcNow ? Math.Min(64.0, _globalXpMultiplier * 2.0) : 2.0;
        _globalXpUntilUtc = DateTime.UtcNow + duration;
        return _globalXpMultiplier;
    }

    /// <summary>Conservé pour compatibilité (anciens appels "XP doublée") — équivaut à <see cref="SetGlobalXp"/> à ×2.</summary>
    public static void ActivateDoubleXp(TimeSpan duration) => SetGlobalXp(2.0, duration);

    public static void ActivateDoubleLoot(TimeSpan duration) => _doubleLootUntilUtc = DateTime.UtcNow + duration;
    public static void ActivateInvasion(KingdomType kingdom, TimeSpan duration) => InvasionUntilUtcByKingdom[kingdom] = DateTime.UtcNow + duration;
    public static void SetCombatsDisabled(bool disabled) => _combatsDisabled = disabled;
    public static bool AreCombatsDisabled => _combatsDisabled;

    /// <summary>Voir demande utilisateur — "augmenter les chances d'avoir des monstres modifiés (shiny etc.) et la capture" : boost temporaire, multiplicateur choisi par l'admin au lancement.</summary>
    public static void ActivateRareBoost(double multiplier, TimeSpan duration)
    {
        _rareBoostMultiplier = Math.Clamp(multiplier, 1.0, 20.0);
        _rareBoostUntilUtc = DateTime.UtcNow + duration;
    }

    public static double XpMultiplier => _globalXpUntilUtc > DateTime.UtcNow ? _globalXpMultiplier : 1.0;
    public static int LootMultiplier => _doubleLootUntilUtc > DateTime.UtcNow ? 2 : 1;

    /// <summary>Multiplicateur appliqué au poids d'apparition des variantes non-Normal (voir MonsterVariantCatalog.RollWeighted) — 1.0 hors boost.</summary>
    public static double RareVariantWeightMultiplier => _rareBoostUntilUtc > DateTime.UtcNow ? _rareBoostMultiplier : 1.0;

    /// <summary>Bonus additif de chance de capture pendant le boost (voir CaptureService) — 0 hors boost, plafonné à +0.5.</summary>
    public static double CaptureChanceBonus => _rareBoostUntilUtc > DateTime.UtcNow ? Math.Clamp((_rareBoostMultiplier - 1.0) * 0.06, 0.0, 0.5) : 0.0;

    /// <summary>Voir GDD/demande utilisateur — "indicateurs visuels quand double XP/loot sont actifs" : consulté par l'endpoint de statut public (voir Server/Program.cs), null si aucun minuteur n'a jamais été activé ou s'il est expiré.</summary>
    public static DateTime? DoubleXpUntilUtc => _globalXpUntilUtc > DateTime.UtcNow ? _globalXpUntilUtc : null;
    public static DateTime? DoubleLootUntilUtc => _doubleLootUntilUtc > DateTime.UtcNow ? _doubleLootUntilUtc : null;

    public static double GlobalXpMultiplier => XpMultiplier;
    public static DateTime? GlobalXpUntilUtc => DoubleXpUntilUtc;
    public static double RareBoostMultiplier => RareVariantWeightMultiplier;
    public static DateTime? RareBoostUntilUtc => _rareBoostUntilUtc > DateTime.UtcNow ? _rareBoostUntilUtc : null;

    public static bool IsInvasionActive(KingdomType kingdom) =>
        InvasionUntilUtcByKingdom.TryGetValue(kingdom, out var until) && until > DateTime.UtcNow;

    /// <summary>Voir GDD/demande utilisateur — "invasion de monstres" : pendant une invasion, les rencontres sauvages du royaume ciblé tirent uniquement des variantes dangereuses (Alpha/Corrompu) au lieu de la pondération normale.</summary>
    public static MonsterVariant RollInvasionVariant(Random random) => random.Next(2) == 0 ? MonsterVariant.Alpha : MonsterVariant.Corrompu;
}
