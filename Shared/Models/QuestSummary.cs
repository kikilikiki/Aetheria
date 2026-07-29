namespace Aetheria.Shared.Models;

/// <summary>Voir GDD/demande utilisateur — "un tutoriel qui force le joueur à faire des quêtes qui lui expliquent le jeu".</summary>
public sealed class QuestSummary
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
}
