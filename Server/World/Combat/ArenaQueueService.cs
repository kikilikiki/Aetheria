using Aetheria.Server.Persistence;
using Aetheria.Shared.Enums;

namespace Aetheria.Server.World.Combat;

/// <summary>
/// File d'attente en mémoire pour les arènes classées (voir GDD — formats 1v1/2v2/3v3/4v4). Un
/// combat n'est formé que lorsqu'assez de joueurs distincts ont rejoint la file pour un format
/// donné ; la première moitié devient l'équipe 0, l'autre moitié l'équipe 1 (voir
/// <c>CombatService.StartArenaMatchAsync</c>). Comme <see cref="CombatSessionStore"/>, un état
/// transitoire conservé en mémoire, pas en base de données.
///
/// Voir Docs/Idees.md — garde-fou anti-auto-appairage (deux personnages du même compte ne
/// peuvent plus se retrouver dans la même file en même temps, ce qui empêche structurellement
/// qu'ils soient répartis sur deux équipes opposées) + file de groupe (un groupe complet occupe
/// un bloc d'une équipe d'un coup via <see cref="_pendingGroups"/> plutôt que d'entrer un par un
/// dans la file individuelle, où ses membres pourraient être séparés entre les deux équipes).
/// </summary>
public sealed class ArenaQueueService
{
    private readonly object _lock = new();
    private readonly Dictionary<ArenaFormat, List<ArenaTicket>> _waiting = new();
    private readonly Dictionary<ArenaFormat, Queue<List<ArenaTicket>>> _pendingGroups = new();
    private readonly Dictionary<Guid, Guid> _matchedCombatByCharacter = new();

    /// <summary>Ajoute un ticket et retourne le groupe complet de tickets si la file atteint le seuil du format, sinon <c>null</c> (le ticket reste en attente). <c>null</c> aussi si ce personnage/compte est déjà en file.</summary>
    public List<ArenaTicket>? EnqueueAndTryMatch(ArenaFormat format, ArenaTicket ticket)
    {
        lock (_lock)
        {
            if (IsAlreadyQueuedLocked(format, ticket.UserId, ticket.CharacterId))
            {
                return null;
            }

            var list = GetOrCreateWaitingListLocked(format);
            list.Add(ticket);
            return TryFormMatchLocked(format);
        }
    }

    /// <summary>
    /// Voir Docs/Idees.md — un groupe complet (récupéré via <c>PartyService</c>) rejoint la file
    /// d'un coup, comme un seul bloc d'équipe, plutôt que membre par membre. <paramref name="groupTickets"/>
    /// doit contenir exactement <see cref="ArenaFormatRules.PlayersPerTeam"/> tickets pour ce
    /// format — sinon la taille du groupe ne correspond pas au format visé.
    /// </summary>
    public List<ArenaTicket>? EnqueueGroupAndTryMatch(ArenaFormat format, IReadOnlyList<ArenaTicket> groupTickets)
    {
        var playersPerTeam = ArenaFormatRules.PlayersPerTeam(format);
        if (groupTickets.Count != playersPerTeam)
        {
            throw new AccountOperationException($"Ce format nécessite un groupe de {playersPerTeam} joueur(s) exactement (groupe actuel : {groupTickets.Count}).");
        }

        lock (_lock)
        {
            foreach (var ticket in groupTickets)
            {
                if (IsAlreadyQueuedLocked(format, ticket.UserId, ticket.CharacterId))
                {
                    throw new AccountOperationException("Un membre du groupe est déjà en file d'attente pour ce format.");
                }
            }

            if (!_pendingGroups.TryGetValue(format, out var groupQueue))
            {
                groupQueue = new Queue<List<ArenaTicket>>();
                _pendingGroups[format] = groupQueue;
            }

            groupQueue.Enqueue(groupTickets.ToList());
            return TryFormMatchLocked(format);
        }
    }

    /// <summary>Doit être appelé sous <see cref="_lock"/>. Priorité : deux groupes en attente se font directement face ; sinon un groupe en attente complète avec des joueurs solo ; sinon appairage solo classique.</summary>
    private List<ArenaTicket>? TryFormMatchLocked(ArenaFormat format)
    {
        var playersPerTeam = ArenaFormatRules.PlayersPerTeam(format);
        var groupQueue = _pendingGroups.TryGetValue(format, out var groups) ? groups : null;
        var list = GetOrCreateWaitingListLocked(format);

        if (groupQueue is { Count: >= 2 })
        {
            var groupA = groupQueue.Dequeue();
            var groupB = groupQueue.Dequeue();
            return [.. groupA, .. groupB];
        }

        if (groupQueue is { Count: 1 } && list.Count >= playersPerTeam)
        {
            var group = groupQueue.Dequeue();
            var solos = list.Take(playersPerTeam).ToList();
            list.RemoveRange(0, playersPerTeam);
            return [.. group, .. solos];
        }

        if ((groupQueue is null || groupQueue.Count == 0) && list.Count >= playersPerTeam * 2)
        {
            var needed = playersPerTeam * 2;
            var matched = list.Take(needed).ToList();
            list.RemoveRange(0, needed);
            return matched;
        }

        return null;
    }

    private bool IsAlreadyQueuedLocked(ArenaFormat format, Guid userId, Guid characterId)
    {
        if (_waiting.TryGetValue(format, out var list) && list.Any(t => t.UserId == userId || t.CharacterId == characterId))
        {
            return true;
        }

        if (_pendingGroups.TryGetValue(format, out var groups) && groups.Any(g => g.Any(t => t.UserId == userId || t.CharacterId == characterId)))
        {
            return true;
        }

        return false;
    }

    private List<ArenaTicket> GetOrCreateWaitingListLocked(ArenaFormat format)
    {
        if (!_waiting.TryGetValue(format, out var list))
        {
            list = [];
            _waiting[format] = list;
        }

        return list;
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

    /// <summary>Retire ce personnage de la file solo, et de tout groupe en attente auquel il appartiendrait — un groupe amputé d'un membre n'a plus la bonne taille pour son format, il est donc retiré en entier (simplification assumée : les autres membres doivent se remettre en file).</summary>
    public void Cancel(Guid characterId)
    {
        lock (_lock)
        {
            foreach (var list in _waiting.Values)
            {
                list.RemoveAll(t => t.CharacterId == characterId);
            }

            foreach (var groupQueue in _pendingGroups.Values)
            {
                if (groupQueue.Any(g => g.Any(t => t.CharacterId == characterId)))
                {
                    var remaining = groupQueue.Where(g => g.All(t => t.CharacterId != characterId)).ToList();
                    groupQueue.Clear();
                    foreach (var group in remaining)
                    {
                        groupQueue.Enqueue(group);
                    }
                }
            }
        }
    }
}
