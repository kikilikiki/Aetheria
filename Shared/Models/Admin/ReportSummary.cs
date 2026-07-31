namespace Aetheria.Shared.Models.Admin;

/// <summary>Voir GDD/demande utilisateur — "les admin peut voir les report sur une page".</summary>
public sealed class ReportSummary
{
    public required Guid Id { get; init; }
    public required Guid ReporterCharacterId { get; init; }
    public required string ReporterCharacterName { get; init; }
    public required Guid ReportedCharacterId { get; init; }
    public required string ReportedCharacterName { get; init; }
    public required string Reason { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required bool Resolved { get; init; }
}
