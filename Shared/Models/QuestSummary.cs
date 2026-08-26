namespace Aetheria.Shared.Models;

/// <summary>Voir GDD/demande utilisateur — "un tutoriel qui force le joueur à faire des quêtes qui lui expliquent le jeu".</summary>
public sealed class QuestSummary
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }

    /// <summary>
    /// Voir Docs/Idees.md — "Embranchements/choix dans la chaîne de quêtes tutoriel". Quand
    /// vrai, <see cref="Id"/>/<see cref="Name"/>/<see cref="Description"/> décrivent l'invite du
    /// choix (pas une vraie quête active) et <see cref="ChoiceOptionAId"/>/<see cref="ChoiceOptionBId"/>
    /// portent les deux options — le client doit appeler <c>POST /api/quests/choose</c> avec
    /// l'une des deux avant qu'une nouvelle quête active ne redevienne disponible.
    /// </summary>
    public bool IsChoice { get; init; }
    public int? ChoiceOptionAId { get; init; }
    public string? ChoiceOptionAName { get; init; }
    public int? ChoiceOptionBId { get; init; }
    public string? ChoiceOptionBName { get; init; }
}
