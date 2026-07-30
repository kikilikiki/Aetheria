namespace Aetheria.Database.Entities;

/// <summary>
/// Suivi d'un défi hebdomadaire/mensuel pour un personnage (table <c>ChallengeProgress</c>) — voir
/// GDD/demande utilisateur "Défis hebdomadaires" + défis mensuels. Une ligne par (personnage,
/// défi, période) : <see cref="BaselineValue"/> est l'instantané de la statistique cumulative
/// concernée (voir <c>StatisticsEntity</c>) pris au tout premier calcul de progression pour cette
/// période, pour dériver un delta "cette semaine/ce mois" sans dupliquer le suivi en cumulatif.
/// </summary>
public sealed class ChallengeProgressEntity
{
    public Guid Id { get; set; }

    public Guid CharacterId { get; set; }
    public CharacterEntity? Character { get; set; }

    public required string ChallengeKey { get; set; }

    /// <summary>Semaine ISO ("AAAA-Wnn") ou mois ("AAAA-Mnn") selon le défi, même style de clé que <c>KingdomWarScheduler</c>.</summary>
    public required string PeriodBucket { get; set; }

    public long BaselineValue { get; set; }
    public bool IsClaimed { get; set; }
}
