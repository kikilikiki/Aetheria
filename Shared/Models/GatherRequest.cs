using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>
/// Corps JSON de <c>POST /api/professions/gather</c> — récolte d'une ressource brute
/// (voir <c>Docs/GameDesign.md</c> — chaîne Mineur → Minerai → Forgeron → ...).
/// </summary>
public sealed class GatherRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required ProfessionType Profession { get; init; }
    public required int ResourceItemId { get; init; }
    public int Quantity { get; init; } = 1;

    /// <summary>
    /// Voir GDD/demande utilisateur — "guerre de territoire... pour que les joueurs de sa team
    /// puissent aller faire des quêtes de minage" : optionnel (récolte encore possible "hors
    /// territoire" pour tout le reste), mais si renseigné et que la mine n'appartient pas au
    /// royaume du personnage, la récolte est refusée — voir ProfessionService.GatherAsync.
    /// </summary>
    public int? TerritoryId { get; init; }
}
