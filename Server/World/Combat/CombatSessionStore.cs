using System.Collections.Concurrent;

namespace Aetheria.Server.World.Combat;

/// <summary>
/// Combats en cours, conservés en mémoire (comme <see cref="Aetheria.Server.Persistence.SessionTokenStore"/>) :
/// un combat est un état transitoire, il n'a pas sa place en base de données.
/// </summary>
public sealed class CombatSessionStore
{
    private readonly ConcurrentDictionary<Guid, CombatSession> _sessions = new();

    public void Add(CombatSession session) => _sessions[session.Id] = session;

    public bool TryGet(Guid id, out CombatSession session) => _sessions.TryGetValue(id, out session!);

    public void Remove(Guid id) => _sessions.TryRemove(id, out _);

    /// <summary>Combat de groupe déjà en cours pour ce groupe, s'il y en a un (voir GDD/demande utilisateur — combat partagé entre membres d'un groupe).</summary>
    public bool TryGetActiveByPartyId(Guid partyId, out CombatSession session)
    {
        session = _sessions.Values.FirstOrDefault(s => s.PartyId == partyId && !s.IsFinished)!;
        return session is not null;
    }
}
