namespace Aetheria.Shared.Models;

/// <summary>Classement des royaumes par points de guerre accumulés cette semaine.</summary>
public sealed record KingdomWarStanding(string KingdomName, long WarPoints);
