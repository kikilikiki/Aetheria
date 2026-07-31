namespace Aetheria.Database.Entities;

/// <summary>
/// Voir GDD/demande utilisateur — "a la fin des 10 etage termine le dongon [...] ajoute un
/// cooldown de 1h avant que il puisse retourne dans le dongon ou il vient d'aller" : une ligne
/// par (personnage, donjon) terminé, recréée/mise à jour à chaque fin de parcours plutôt qu'un
/// historique complet.
/// </summary>
public sealed class DungeonCooldownEntity
{
    public Guid Id { get; set; }
    public Guid CharacterId { get; set; }
    public int DungeonId { get; set; }
    public DateTime AvailableAtUtc { get; set; }
}
