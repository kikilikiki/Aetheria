namespace Aetheria.Shared.Models.Combat;

/// <summary>
/// Corps JSON de <c>POST /api/pvp/challenge</c> : défie directement un autre personnage en duel
/// (voir <c>Docs/GameDesign.md</c> — section PvP). Pas de file d'attente/matchmaking pour cette
/// première version, un défi direct entre deux personnages connus.
/// </summary>
public sealed class StartPvpCombatRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required IReadOnlyList<Guid> MonsterIds { get; init; }
    public required Guid OpponentCharacterId { get; init; }
    public required IReadOnlyList<Guid> OpponentMonsterIds { get; init; }
}
