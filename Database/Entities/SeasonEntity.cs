namespace Aetheria.Database.Entities;

/// <summary>
/// Saison de jeu (table <c>Seasons</c>, voir <c>Docs/GameDesign.md</c> — section Saisons :
/// 3 à 4 mois, nouveau contenu, classements PvP remis à zéro). Le contenu ajouté à chaque
/// saison (monstres, donjons, cosmétiques) reste un travail de contenu, pas de code — cette
/// entité ne fait que suivre quelle saison est active et depuis quand.
/// </summary>
public sealed class SeasonEntity
{
    public int Id { get; set; }
    public int Number { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public bool IsActive { get; set; }
}
