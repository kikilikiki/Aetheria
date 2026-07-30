using Aetheria.Shared.Enums;

namespace Aetheria.Database.Entities;

/// <summary>
/// Boss mondial (table <c>WorldBosses</c>) — voir GDD/demande utilisateur "un boss monde ou le
/// but est de faire un max de degat, plus on fait de degat plus on a de point, il a une barre de
/// vie et peut etre tue". Un seul actif à la fois (voir <see cref="Server.World.WorldBossService"/>).
/// </summary>
public sealed class WorldBossEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public int MaxHealth { get; set; }
    public int CurrentHealth { get; set; }
    public bool IsAlive { get; set; } = true;
    public DateTime SpawnedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? KilledAtUtc { get; set; }
    public string? KillerCharacterName { get; set; }

    /// <summary>Voir GDD/demande utilisateur — "boss geant mondial [invoque] a un royaume" : purement informatif (le combat contre le boss reste accessible à tous, voir WorldBossService), affiché dans l'UI.</summary>
    public KingdomType? TargetKingdom { get; set; }

    /// <summary>
    /// Voir GDD/demande utilisateur — "refonte du spawn de boss mondial (par ID, pas juste un
    /// texte)" : invoqué à partir d'une espèce déjà existante du catalogue plutôt que d'un nom
    /// libre — donne un portrait/Element réels au boss côté client. Nom/Element dénormalisés à
    /// l'invocation (voir WorldBossService.SpawnAsync) pour ne pas dépendre d'une jointure à
    /// chaque lecture de statut.
    /// </summary>
    public int? SpeciesId { get; set; }
    public Element BossElement { get; set; } = Element.Neutre;

    /// <summary>Voir GDD/demande utilisateur — "la recompense va au royaume qui inflige le plus de degats" : renseigné à la mort du boss (voir WorldBossService.AttackAsync), null tant qu'il est vivant.</summary>
    public KingdomType? WinningKingdom { get; set; }

    public List<WorldBossDamageEntity> DamageEntries { get; set; } = [];
}
