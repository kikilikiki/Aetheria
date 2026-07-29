using Aetheria.Shared.Enums;

namespace Aetheria.Server.World.Combat;

/// <summary>
/// File d'attente en mémoire pour les combats de guerre de royaumes (voir GDD/demande utilisateur
/// — bâtiment "Guerre", UI "prêt", matchmaking contre un autre royaume). Même mécanique
/// qu'<see cref="ArenaQueueService"/> mais l'appairage se fait entre deux royaumes DIFFÉRENTS
/// plutôt qu'un format à effectif fixe — le premier ticket d'un autre royaume déjà en attente
/// forme immédiatement un duel 1v1 (voir <c>CombatService.StartFriendlyTeamDuelAsync</c>, dont la
/// victoire alimente déjà les points de guerre du royaume vainqueur via
/// <c>ApplyArenaResultAsync</c>).
/// </summary>
public sealed class KingdomWarQueueService
{
    public sealed record WarTicket(Guid CharacterId, Guid UserId, KingdomType Kingdom);

    private readonly object _lock = new();
    private readonly List<WarTicket> _waiting = [];
    private readonly Dictionary<Guid, Guid> _matchedCombatByCharacter = new();

    /// <summary>Ajoute un ticket et retourne la paire (adversaire, soi-même) si un royaume différent attendait déjà, sinon <c>null</c> (le ticket reste en attente).</summary>
    public List<WarTicket>? EnqueueAndTryMatch(WarTicket ticket)
    {
        lock (_lock)
        {
            if (_waiting.Any(t => t.CharacterId == ticket.CharacterId))
            {
                return null;
            }

            var opponent = _waiting.FirstOrDefault(t => t.Kingdom != ticket.Kingdom);
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
