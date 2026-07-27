namespace Aetheria.Shared.Models;

/// <summary>
/// Statistiques de combat communes aux personnages et aux créatures
/// (voir <c>Docs/GameDesign.md</c> — section Monstres / Statistiques).
/// </summary>
public readonly record struct StatBlock(
    int Health,
    int Attack,
    int Defense,
    int Speed,
    int Intelligence,
    int Resistance)
{
    public static StatBlock Zero { get; } = new(0, 0, 0, 0, 0, 0);

    public static StatBlock operator +(StatBlock a, StatBlock b) => new(
        a.Health + b.Health,
        a.Attack + b.Attack,
        a.Defense + b.Defense,
        a.Speed + b.Speed,
        a.Intelligence + b.Intelligence,
        a.Resistance + b.Resistance);
}
