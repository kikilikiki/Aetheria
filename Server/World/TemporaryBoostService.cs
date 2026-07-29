using Aetheria.Database.Entities;

namespace Aetheria.Server.World;

/// <summary>
/// Voir GDD/demande utilisateur — "ajoute des consommables pour booster la luck l'xp la money" :
/// trois potions temporaires, indépendantes du grade payant (voir <see cref="PremiumService"/>),
/// consommées via <c>/use &lt;idObjet&gt;</c> (voir PlayerSession, ConsumableService). Une simple
/// date d'expiration par personnage plutôt qu'un compteur de charges — reboire la même potion
/// prolonge simplement l'effet (repart de maintenant + la durée complète).
/// </summary>
public static class TemporaryBoostService
{
    public const double BoostMultiplier = 1.5;
    public static readonly TimeSpan BoostDuration = TimeSpan.FromMinutes(30);

    public static double XpMultiplier(CharacterEntity character) =>
        character.XpBoostExpiresAtUtc > DateTime.UtcNow ? BoostMultiplier : 1.0;

    public static double GoldMultiplier(CharacterEntity character) =>
        character.GoldBoostExpiresAtUtc > DateTime.UtcNow ? BoostMultiplier : 1.0;

    public static bool HasLuckBoost(CharacterEntity character) =>
        character.LuckBoostExpiresAtUtc > DateTime.UtcNow;
}
