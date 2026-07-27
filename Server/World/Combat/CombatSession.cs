namespace Aetheria.Server.World.Combat;

/// <summary>État complet (serveur, mutable) d'un combat en cours, conservé en mémoire pour sa durée de vie.</summary>
public sealed class CombatSession
{
    public const int GridWidth = 7;
    public const int GridHeight = 7;

    public required Guid Id { get; init; }
    public required Guid OwnerUserId { get; init; }
    public required Guid CharacterId { get; init; }

    public List<Combatant> Combatants { get; set; } = [];
    public int TurnIndex { get; set; }
    public bool IsFinished { get; set; }
    public int? WinningTeam { get; set; }
    public string? LastMessage { get; set; }

    public Combatant? CurrentCombatant => Combatants.Count == 0 ? null : Combatants[TurnIndex % Combatants.Count];
}
