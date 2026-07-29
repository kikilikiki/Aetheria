namespace Aetheria.Database.Entities;

/// <summary>Définition d'une quête (table <c>Quests</c>) — catalogue de contenu.</summary>
public sealed class QuestEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? KingdomId { get; set; }

    public long RewardGold { get; set; }
    public long RewardExperience { get; set; }

    /// <summary>
    /// Voir GDD/demande utilisateur — "un tutoriel qui force le joueur à faire des quêtes qui lui
    /// expliquent le jeu" et "une histoire avec des dialogues cohérents" : chaîne strictement
    /// linéaire (une seule quête "active" à la fois, la première non complétée par ordre
    /// croissant) plutôt qu'un journal à embranchements — voir QuestService.GetActiveQuestAsync.
    /// </summary>
    public int SequenceOrder { get; set; }
}
