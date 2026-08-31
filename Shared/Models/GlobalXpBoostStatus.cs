namespace Aetheria.Shared.Models;

/// <summary>
/// Voir demande utilisateur — "ajoute un achat en gemmes de X2 XP global puis on peut repayer en
/// gemmes pour X4 puis X8 etc de plus en plus cher". État renvoyé par
/// <c>GET /api/shop/xp-boost/status</c> et <c>POST /api/shop/xp-boost/buy</c>.
/// </summary>
public sealed record GlobalXpBoostStatus(
    double CurrentMultiplier,
    DateTime? UntilUtc,
    double NextMultiplier,
    long NextTierGemCost);
