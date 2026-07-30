namespace Aetheria.Database.Entities;

/// <summary>
/// Catalogue des donjons (table <c>Dungeons</c>) — chaque donjon représente une région entière
/// d'un royaume (voir <c>Docs/GameDesign.md</c> — section Donjons). <see cref="Seed"/> pilote la
/// génération procédurale déterministe des étages (voir <c>Server/World/DungeonFloorGenerator</c>).
/// </summary>
public sealed class DungeonEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int KingdomId { get; set; }
    public KingdomEntity? Kingdom { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Seed { get; set; }

    // Position dynamique sur la carte du monde (voir GDD — "les donjons n'ont pas toujours un
    // emplacement fixe", rotation toutes les heures). PositionHourBucket identifie l'heure UTC
    // (tronquée) pour laquelle WorldX/WorldY ont été calculés — voir DungeonWorldService.
    public int WorldX { get; set; }
    public int WorldY { get; set; }
    public long PositionHourBucket { get; set; } = -1;

    /// <summary>Voir GDD/demande utilisateur — "de nouveaux donjons avec leur niveau min pour rentrer" : 1 = accessible à tout le monde (voir GDD — "toujours 1 donjon de niveau 1 min pour les débutants").</summary>
    public int MinLevel { get; set; } = 1;

    /// <summary>Voir GDD/demande utilisateur — "des donjons hardcore (niv 15+) et en dessous il n'y aura pas de hardcore" : monstres à statistiques majorées (voir CombatService.StartFromDungeonAsync). Toujours accompagné de <see cref="MinLevel"/> ≥ 15.</summary>
    public bool IsHardcore { get; set; }

    /// <summary>
    /// Voir GDD/demande utilisateur — "contenu end-game... donjons mythiques avec modificateurs...
    /// boss impossibles" : gardé en plus de <see cref="MinLevel"/> par <c>EndGameService</c>
    /// (voir CombatService.StartFromDungeonAsync) — réservé aux comptes possédant déjà chaque
    /// espèce de créature au niveau maximum et chaque succès de gameplay.
    /// </summary>
    public bool IsMythic { get; set; }

    /// <summary>Voir GDD/demande utilisateur — "modificateurs" : texte affiché au joueur, statistiques des monstres rencontrés majorées ×3 (voir CombatService.StartAsync).</summary>
    public string MythicModifierDescription { get; set; } = string.Empty;
}
