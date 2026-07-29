using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>
/// Niveau/XP d'un personnage dans un métier donné, y compris ceux jamais pratiqués (niveau 1, 0
/// XP) — voir GDD/demande utilisateur "un UI avec un bouton pour voir les métiers, les niveaux de
/// chaque métier". Un par <see cref="ProfessionType"/>, voir <c>ProfessionService.GetSummaryAsync</c>.
/// </summary>
public sealed class ProfessionSummary
{
    public required ProfessionType Profession { get; init; }
    public required int Level { get; init; }
    public required long Experience { get; init; }
    public required long ExperienceForNextLevel { get; init; }
}
