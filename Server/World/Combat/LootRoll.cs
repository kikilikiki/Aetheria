namespace Aetheria.Server.World.Combat;

/// <summary>
/// Résolution des réclamations de butin (voir GDD — "si deux joueurs choisissent le même objet,
/// il sera attribué aléatoirement"). Fonction pure, testable indépendamment de la base de
/// données/session HTTP.
/// </summary>
public static class LootRoll
{
    /// <summary>
    /// <paramref name="claims"/> : personnage -> index de l'objet réclamé. Retourne, pour chaque
    /// index d'objet réclamé par au moins un personnage, le personnage gagnant (tirage aléatoire
    /// uniquement s'il y a plusieurs réclamants sur le même objet).
    /// </summary>
    public static Dictionary<int, Guid> Resolve(IReadOnlyDictionary<Guid, int> claims, Random random)
    {
        var winners = new Dictionary<int, Guid>();

        foreach (var group in claims.GroupBy(kv => kv.Value))
        {
            var claimants = group.Select(kv => kv.Key).ToList();
            winners[group.Key] = claimants.Count == 1 ? claimants[0] : claimants[random.Next(claimants.Count)];
        }

        return winners;
    }
}
