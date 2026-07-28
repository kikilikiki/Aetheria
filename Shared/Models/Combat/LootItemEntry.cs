namespace Aetheria.Shared.Models.Combat;

/// <summary>Un des objets proposés dans un butin de fin de combat (voir <c>LootSessionState</c>).</summary>
public sealed class LootItemEntry
{
    public required int Index { get; init; }
    public required int ItemId { get; init; }
    public required string Name { get; init; }
}
