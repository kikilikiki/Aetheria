using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models.Combat;

/// <summary>
/// Corps JSON de <c>POST /api/pvp/arena/queue-party</c> — voir Docs/Idees.md "vrai lobby
/// d'arène" : le groupe du personnage appelant rejoint la file d'arène comme un seul bloc
/// d'équipe (voir <c>ArenaQueueService.EnqueueGroupAndTryMatch</c>) plutôt que chaque membre
/// individuellement. Chaque membre engage son équipe active (<c>MonsterEntity.EquippedSlot</c>),
/// même principe que <c>CombatService.StartFriendlyTeamDuelAsync</c> — pas de sélection
/// manuelle par combat, impossible à coordonner entre plusieurs joueurs humains avant le début
/// du match.
/// </summary>
public sealed class QueueGroupForArenaRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required ArenaFormat Format { get; init; }
}
