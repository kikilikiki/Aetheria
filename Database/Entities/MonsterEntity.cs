using Aetheria.Shared.Enums;

namespace Aetheria.Database.Entities;

/// <summary>
/// Créature possédée par un joueur (table <c>Monsters</c>). <see cref="SpeciesId"/> référence
/// une <see cref="Aetheria.Shared.Models.MonsterSpeciesData"/> du catalogue de contenu
/// (statique, hors base de données — voir MonsterEditor).
/// </summary>
public sealed class MonsterEntity
{
    public Guid Id { get; set; }

    public Guid OwnerCharacterId { get; set; }
    public CharacterEntity? OwnerCharacter { get; set; }

    public int SpeciesId { get; set; }
    public MonsterVariant Variant { get; set; } = MonsterVariant.Normal;

    public string Nickname { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public int Experience { get; set; }
    public string Personality { get; set; } = string.Empty;
    public string PassiveTalent { get; set; } = string.Empty;

    /// <summary>Fait partie de l'équipe active (4 créatures maximum combattent — voir GDD).</summary>
    public bool IsInActiveTeam { get; set; }

    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
}
