namespace Aetheria.Shared.Models.Combat;

/// <summary>
/// État d'un butin de fin de combat (voir GDD — 4 objets à départager, tirage aléatoire en cas
/// d'égalité). <see cref="Winners"/> n'est renseigné qu'une fois <see cref="IsResolved"/> vrai.
/// </summary>
public sealed class LootSessionState
{
    public required Guid LootId { get; init; }
    public required IReadOnlyList<LootItemEntry> Items { get; init; }
    public required IReadOnlyList<Guid> EligibleCharacterIds { get; init; }
    public required IReadOnlyList<Guid> ClaimedCharacterIds { get; init; }
    public required bool IsResolved { get; init; }
    public IReadOnlyDictionary<int, Guid>? Winners { get; init; }
}
