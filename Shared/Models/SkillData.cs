using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>
/// Compétence/sort utilisable en combat tactique sur grille : coût en points d'action,
/// portée et zone d'effet définissent son usage tactique.
/// </summary>
public sealed class SkillData
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public Element Element { get; init; } = Element.Neutre;

    /// <summary>Portée en nombre de cases sur la grille.</summary>
    public int Range { get; init; } = 1;

    /// <summary>Rayon de la zone d'effet en cases (0 = case unique).</summary>
    public int AreaOfEffect { get; init; }

    public int Damage { get; init; }
    public int Healing { get; init; }
    public int ActionPointCost { get; init; } = 1;

    /// <summary>Nombre de tours avant de pouvoir réutiliser la compétence.</summary>
    public int CooldownTurns { get; init; }
}
