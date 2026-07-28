using Aetheria.Shared.Enums;

namespace Aetheria.Server.World.Combat;

/// <summary>
/// File d'attente en mémoire pour les arènes classées (voir GDD — formats 1v1/2v2/3v3/4v4). Un
/// combat n'est formé que lorsqu'assez de joueurs distincts ont rejoint la file pour un format
/// donné ; la première moitié devient l'équipe 0, l'autre moitié l'équipe 1 (voir
/// <c>CombatService.StartArenaMatchAsync</c>). Comme <see cref="CombatSessionStore"/>, un état
/// transitoire conservé en mémoire, pas en base de données.
/// </summary>
public sealed class ArenaQueueService
{
    private readonly object _lock = new();
    private readonly Dictionary<ArenaFormat, List<ArenaTicket>> _waiting = new();
    private readonly Dictionary<Guid, Guid> _matchedCombatByCharacter = new();

    /// <summary>Ajoute un ticket et retourne le groupe complet de tickets si la file atteint le seuil du format, sinon <c>null</c> (le ticket reste en attente).</summary>
    public List<ArenaTicket>? EnqueueAndTryMatch(ArenaFormat format, ArenaTicket ticket)
    {
        lock (_lock)
        {
            if (!_waiting.TryGetValue(format, out var list))
            {
                list = [];
                _waiting[format] = list;
            }

            if (list.Any(t => t.CharacterId == ticket.CharacterId))
            {
                return null;
            }

            list.Add(ticket);

            var needed = ArenaFormatRules.PlayersPerTeam(format) * 2;
            if (list.Count < needed)
            {
                return null;
            }

            var matched = list.Take(needed).ToList();
            list.RemoveRange(0, needed);
            return matched;
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

    /// <summary>Consomme (retire) le combat assigné à ce personnage s'il en a un — chaque appairage n'est renvoyé qu'une fois.</summary>
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
            foreach (var list in _waiting.Values)
            {
                list.RemoveAll(t => t.CharacterId == characterId);
            }
        }
    }
}
