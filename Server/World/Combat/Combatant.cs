namespace Aetheria.Server.World.Combat;

/// <summary>
/// Représentation interne (mutable) d'un combattant pendant la résolution d'un combat.
/// N'est jamais exposée telle quelle au client — voir <see cref="Aetheria.Shared.Models.Combat.CombatantState"/>.
/// </summary>
public sealed class Combatant
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required int Team { get; init; }
    public int X { get; set; }
    public int Y { get; set; }
    public required int MaxHealth { get; init; }
    public int CurrentHealth { get; set; }
    public required int Attack { get; init; }
    public required int Defense { get; init; }
    public required int Speed { get; init; }
    public required int MovementRange { get; init; }
    public required int AttackRange { get; init; }
    public bool IsPlayerControlled { get; init; }

    public bool IsAlive => CurrentHealth > 0;
}
