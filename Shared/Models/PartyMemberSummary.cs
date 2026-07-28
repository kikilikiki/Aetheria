namespace Aetheria.Shared.Models;

/// <summary>Membre d'un groupe tel qu'exposé au client (voir <c>PartySummary</c>).</summary>
public sealed class PartyMemberSummary
{
    public required Guid CharacterId { get; init; }
    public required string Name { get; init; }
    public required int Level { get; init; }
}
