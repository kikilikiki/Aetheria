using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>
/// Description d'une espèce de créature (la "fiche" du bestiaire) — indépendante des
/// individus possédés par les joueurs, voir <see cref="MonsterInstanceData"/>.
/// </summary>
public sealed class MonsterSpeciesData
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public Element Element { get; init; } = Element.Neutre;
    public MonsterType Type { get; init; } = MonsterType.Guerrier;
    public Rarity BaseRarity { get; init; } = Rarity.Commun;
    public string Habitat { get; init; } = string.Empty;
    public string Lore { get; init; } = string.Empty;
    public StatBlock BaseStats { get; init; } = StatBlock.Zero;
    public IReadOnlyList<int> SkillIds { get; init; } = Array.Empty<int>();

    /// <summary>Espèce vers laquelle celle-ci évolue, ou <c>null</c> si elle n'évolue pas.</summary>
    public int? EvolvesIntoSpeciesId { get; init; }

    public int EvolutionLevel { get; init; }
}
