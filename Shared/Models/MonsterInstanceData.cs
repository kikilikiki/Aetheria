using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>
/// Créature effectivement possédée par un joueur : une instance capturée d'une
/// <see cref="MonsterSpeciesData"/>, avec sa propre progression.
/// </summary>
public sealed class MonsterInstanceData
{
    public required Guid Id { get; init; }
    public required int SpeciesId { get; init; }
    public required Guid OwnerCharacterId { get; init; }

    public MonsterVariant Variant { get; set; } = MonsterVariant.Normal;
    public string Nickname { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public int Experience { get; set; }
    public string Personality { get; set; } = string.Empty;
    public string PassiveTalent { get; set; } = string.Empty;

    /// <summary>Composante de l'équipe active (4 créatures maximum participent au combat).</summary>
    public bool IsInActiveTeam { get; set; }

    public DateTime CapturedAtUtc { get; init; } = DateTime.UtcNow;
}
