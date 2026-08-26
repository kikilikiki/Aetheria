namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/quests/choose</c> — voir Docs/Idees.md, embranchement de quête.</summary>
public sealed class ChooseQuestRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required int ChosenQuestId { get; init; }
}
