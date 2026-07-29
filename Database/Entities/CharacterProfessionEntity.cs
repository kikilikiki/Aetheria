using Aetheria.Shared.Enums;

namespace Aetheria.Database.Entities;

/// <summary>Progression d'un personnage dans un métier (voir <c>Docs/GameDesign.md</c> — section Métiers).</summary>
public sealed class CharacterProfessionEntity
{
    public Guid Id { get; set; }

    public Guid CharacterId { get; set; }
    public CharacterEntity? Character { get; set; }

    public ProfessionType Profession { get; set; }
    public int Level { get; set; } = 1;
    public long Experience { get; set; }

    /// <summary>Voir GDD/demande utilisateur — "ajoute un cooldown après extraction à un endroit (mine par exemple)" — voir ProfessionService.GatherAsync.</summary>
    public DateTime? LastGatheredAtUtc { get; set; }
}
