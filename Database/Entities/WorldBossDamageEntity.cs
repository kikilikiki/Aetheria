namespace Aetheria.Database.Entities;

/// <summary>
/// Dégâts cumulés d'un personnage sur une instance de boss mondial (table
/// <c>WorldBossDamageEntries</c>) — voir GDD/demande utilisateur "un leaderboard pour les gens qui
/// on fait le plus de point (du boss actuel et de toujours)". Le classement "de toujours" est une
/// simple somme groupée par personnage sur toutes les lignes de toutes les instances de boss
/// (voir <see cref="Server.World.WorldBossService"/>), pas une table séparée.
/// </summary>
public sealed class WorldBossDamageEntity
{
    public Guid Id { get; set; }

    public Guid WorldBossId { get; set; }
    public WorldBossEntity? WorldBoss { get; set; }

    public Guid CharacterId { get; set; }
    public required string CharacterName { get; set; }
    public long TotalDamage { get; set; }

    /// <summary>Voir GDD/demande utilisateur — "on peut attaquer plusieurs fois le boss monde, limite le a 3" : nombre de combats déjà engagés contre cette instance de boss (voir <see cref="Server.World.WorldBossService.MaxAttempts"/>).</summary>
    public int AttackCount { get; set; }
}
