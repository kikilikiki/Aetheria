namespace Aetheria.Database.Entities;

/// <summary>
/// Voir GDD/demande utilisateur — "fait en sorte que les dongon normal on est 3 vie" : compteur de
/// vies par personnage x donjon (table <c>DungeonLives</c>), uniquement pour les tentatives en mode
/// normal (le hardcore/mythique n'en consomme pas, voir CombatService.ApplyPveVictoryRewardsAsync).
/// Réinitialisé une fois par jour UTC (voir <c>LastResetUtc</c>, vérifié paresseusement à la
/// lecture plutôt que via un job planifié — même idiome que <c>WeeklyChestService.CurrentWeekBucket</c>
/// pour le pas de temps, en plus court).
/// </summary>
public sealed class DungeonLivesEntity
{
    public Guid Id { get; set; }

    public Guid CharacterId { get; set; }
    public int DungeonId { get; set; }

    public int LivesRemaining { get; set; } = MaxLives;
    public DateTime LastResetUtc { get; set; } = DateTime.UtcNow;

    public const int MaxLives = 3;
}
