namespace Aetheria.Shared.Models.BattlePass;

/// <summary>
/// Un palier de la route du Passe de Niveau (voir GDD/demande utilisateur — "améliore l'ui du
/// passe pour faire une route que l'on peut scroll") : description des récompenses gratuite et
/// premium à ce niveau, et si ce palier a déjà été atteint par le personnage — voir
/// <c>BattlePassService.ToStatus</c> pour le calcul (mêmes formules que
/// <c>GrantFreeRewardAsync</c>/<c>GrantPremiumRewardAsync</c>, purement descriptif ici, aucune
/// remise de récompense).
/// </summary>
public sealed class BattlePassTier
{
    public required int Level { get; init; }
    public required string FreeReward { get; init; }
    public required string PremiumReward { get; init; }
    public required bool IsReached { get; init; }
}
