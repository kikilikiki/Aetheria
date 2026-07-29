namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/quests/complete</c> — voir GDD/demande utilisateur, tutoriel/histoire par quêtes.</summary>
public sealed class CompleteQuestRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required string QuestName { get; init; }
}
