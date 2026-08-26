namespace Aetheria.Shared.Models;

/// <summary>Voir Docs/Idees.md — état de l'arbre de talents d'une créature, renvoyé par <c>GET /api/monsters/{id}/talents</c>.</summary>
public sealed class MonsterTalentStatus
{
    public required Guid MonsterId { get; init; }
    public required int TalentPoints { get; init; }
    public required IReadOnlyList<string> UnlockedNodeKeys { get; init; }
}
