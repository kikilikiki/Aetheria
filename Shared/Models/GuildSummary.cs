namespace Aetheria.Shared.Models;

/// <summary>Réponse JSON décrivant une guilde et ses membres.</summary>
public sealed class GuildSummary
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required int Level { get; init; }
    public required long TreasuryGold { get; init; }
    public required Guid LeaderCharacterId { get; init; }
    public required IReadOnlyList<string> MemberNames { get; init; }

    /// <summary>Voir GDD/demande utilisateur — "Niveau de guilde".</summary>
    public long GuildExperience { get; init; }
    public long ExperienceForNextLevel { get; init; }

    /// <summary>Voir GDD/demande utilisateur — "Guerres de guildes".</summary>
    public long WarPoints { get; init; }

    /// <summary>Voir GDD/demande utilisateur — "Quêtes de guilde" : objectif hebdomadaire "déposer des objets au coffre partagé".</summary>
    public int WeeklyQuestItemsDeposited { get; init; }
    public int WeeklyQuestItemTarget { get; init; }
    public bool WeeklyQuestCompleted { get; init; }
}
