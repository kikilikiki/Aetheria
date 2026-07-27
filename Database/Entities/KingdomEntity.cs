using Aetheria.Shared.Enums;

namespace Aetheria.Database.Entities;

/// <summary>Royaume (table <c>Kingdoms</c>) : un par valeur de <see cref="KingdomType"/>.</summary>
public sealed class KingdomEntity
{
    public int Id { get; set; }
    public KingdomType Type { get; set; }
    public required string Name { get; set; }
    public string CapitalName { get; set; } = string.Empty;
}
