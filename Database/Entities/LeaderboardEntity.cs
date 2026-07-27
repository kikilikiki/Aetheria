using Aetheria.Shared.Enums;

namespace Aetheria.Database.Entities;

/// <summary>
/// Score d'un personnage dans un classement donné (table <c>Leaderboard</c>). Une ligne par
/// couple (personnage, catégorie) ; recalculée périodiquement côté serveur.
/// </summary>
public sealed class LeaderboardEntity
{
    public Guid Id { get; set; }

    public Guid CharacterId { get; set; }
    public CharacterEntity? Character { get; set; }

    public LeaderboardCategory Category { get; set; }
    public long Score { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
