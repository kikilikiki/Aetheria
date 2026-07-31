namespace Aetheria.Database.Entities;

/// <summary>
/// Signalement d'un joueur (table <c>Reports</c>) — voir GDD/demande utilisateur "ajoute la
/// possibilité de report un joueur". Noms dénormalisés (voir <see cref="ReporterCharacterName"/>/
/// <see cref="ReportedCharacterName"/>) pour un affichage immédiat côté admin sans jointure, comme
/// <see cref="WeeklyChestEntity.ClaimedByCharacterName"/>.
/// </summary>
public sealed class ReportEntity
{
    public Guid Id { get; set; }

    public Guid ReporterCharacterId { get; set; }
    public required string ReporterCharacterName { get; set; }

    public Guid ReportedCharacterId { get; set; }
    public required string ReportedCharacterName { get; set; }

    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Voir GDD/demande utilisateur — permet à l'admin de marquer un signalement comme traité sans le supprimer.</summary>
    public bool Resolved { get; set; }
}
