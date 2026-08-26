namespace Aetheria.Shared.Models;

/// <summary>Voir Docs/Idees.md — "PvP sauvage" : réputation/grade militaire d'un personnage, renvoyé par <c>GET /api/pvp/wild/reputation</c>.</summary>
public sealed class MilitaryReputationStatus
{
    public required int Reputation { get; init; }
    public required string Rank { get; init; }
}
