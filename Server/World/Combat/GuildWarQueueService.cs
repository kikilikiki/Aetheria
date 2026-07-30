namespace Aetheria.Server.World.Combat;

/// <summary>
/// Voir GDD/demande utilisateur — "Guerres de guildes". File d'attente en mémoire, même mécanique
/// qu'<see cref="KingdomWarQueueService"/> mais l'appairage se fait entre deux GUILDES différentes
/// (ou "sans guilde" traité comme non appairable entre membres de la même guilde) plutôt qu'entre
/// royaumes — le combat lui-même reste un duel amical 1v1 (voir
/// <c>CombatService.StartFriendlyTeamDuelAsync</c>), dont la victoire alimente les points de
/// guerre de la guilde du vainqueur (voir <c>GuildService.AwardWarPointsAsync</c>).
/// </summary>
public sealed class GuildWarQueueService
{
    public sealed record WarTicket(Guid CharacterId, Guid UserId, Guid GuildId);

    private readonly object _lock = new();
    private readonly List<WarTicket> _waiting = [];
    private readonly Dictionary<Guid, Guid> _matchedCombatByCharacter = new();

    public List<WarTicket>? EnqueueAndTryMatch(WarTicket ticket)
    {
        lock (_lock)
        {
            if (_waiting.Any(t => t.CharacterId == ticket.CharacterId))
            {
                return null;
            }

            var opponent = _waiting.FirstOrDefault(t => t.GuildId != ticket.GuildId);
            if (opponent is null)
            {
                _waiting.Add(ticket);
                return null;
            }

            _waiting.Remove(opponent);
            return [opponent, ticket];
        }
    }

    public void RecordMatch(IEnumerable<Guid> characterIds, Guid combatId)
    {
        lock (_lock)
        {
            foreach (var id in characterIds)
            {
                _matchedCombatByCharacter[id] = combatId;
            }
        }
    }

    public bool TryConsumeMatch(Guid characterId, out Guid combatId)
    {
        lock (_lock)
        {
            return _matchedCombatByCharacter.Remove(characterId, out combatId);
        }
    }

    public void Cancel(Guid characterId)
    {
        lock (_lock)
        {
            _waiting.RemoveAll(t => t.CharacterId == characterId);
        }
    }
}
