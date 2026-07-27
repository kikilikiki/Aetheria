namespace Aetheria.Database.Entities;

/// <summary>
/// Succès débloqué par un compte (table <c>Achievements</c>). <see cref="AchievementKey"/>
/// référence une définition du catalogue de contenu (nom, catégorie, récompense) — voir
/// <c>Docs/GameDesign.md</c> — section Succès.
/// </summary>
public sealed class AchievementEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }

    public required string AchievementKey { get; set; }
    public DateTime UnlockedAtUtc { get; set; } = DateTime.UtcNow;
}
