namespace Aetheria.Shared.Models;

/// <summary>Réponse JSON décrivant un groupe et ses membres (voir GDD — XP partagée en groupe).</summary>
public sealed class PartySummary
{
    public required Guid Id { get; init; }
    public required Guid LeaderCharacterId { get; init; }
    public required IReadOnlyList<PartyMemberSummary> Members { get; init; }
}
