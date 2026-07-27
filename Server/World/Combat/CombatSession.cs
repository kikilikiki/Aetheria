namespace Aetheria.Server.World.Combat;

/// <summary>État complet (serveur, mutable) d'un combat en cours, conservé en mémoire pour sa durée de vie.</summary>
public sealed class CombatSession
{
    public const int GridWidth = 7;
    public const int GridHeight = 7;

    public required Guid Id { get; init; }

    /// <summary>Mode PvE (contre un monstre sauvage) ou PvP (contre un autre joueur) — change les règles applicables (capture, etc.).</summary>
    public required bool IsPvp { get; init; }

    /// <summary>Compte propriétaire de chaque équipe jouable (les monstres sauvages, contrôlés par l'IA, n'y figurent pas).</summary>
    public Dictionary<int, Guid> TeamOwnerUserId { get; init; } = [];

    /// <summary>Personnage représentant chaque équipe jouable (pour attribuer capture/récompenses/statistiques).</summary>
    public Dictionary<int, Guid> TeamCharacterId { get; init; } = [];

    public List<Combatant> Combatants { get; set; } = [];
    public int TurnIndex { get; set; }
    public bool IsFinished { get; set; }
    public int? WinningTeam { get; set; }
    public string? LastMessage { get; set; }

    public Combatant? CurrentCombatant => Combatants.Count == 0 ? null : Combatants[TurnIndex % Combatants.Count];
}
