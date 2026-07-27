using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>Personnage jouable appartenant à un compte.</summary>
public sealed class CharacterData
{
    public required Guid Id { get; init; }
    public required Guid AccountId { get; init; }
    public required string Name { get; init; }
    public CharacterClass Class { get; init; }
    public KingdomType Kingdom { get; init; }

    public int Level { get; set; } = 1;
    public long Experience { get; set; }
    public long Gold { get; set; }

    /// <summary>Jusqu'à 4 créatures participant au combat (voir <c>Docs/GameDesign.md</c> — Combats).</summary>
    public IReadOnlyList<Guid> ActiveMonsterIds { get; set; } = Array.Empty<Guid>();
}
