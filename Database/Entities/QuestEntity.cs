namespace Aetheria.Database.Entities;

/// <summary>Définition d'une quête (table <c>Quests</c>) — catalogue de contenu.</summary>
public sealed class QuestEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? KingdomId { get; set; }

    public long RewardGold { get; set; }
    public long RewardExperience { get; set; }
}
