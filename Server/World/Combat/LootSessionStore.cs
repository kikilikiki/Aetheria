using System.Collections.Concurrent;

namespace Aetheria.Server.World.Combat;

/// <summary>Butins en cours de répartition, conservés en mémoire (même logique que <see cref="CombatSessionStore"/>).</summary>
public sealed class LootSessionStore
{
    private readonly ConcurrentDictionary<Guid, LootSession> _sessions = new();

    public void Add(LootSession session) => _sessions[session.Id] = session;

    public bool TryGet(Guid id, out LootSession session) => _sessions.TryGetValue(id, out session!);

    public void Remove(Guid id) => _sessions.TryRemove(id, out _);

    /// <summary>Tous les butins en cours de répartition — utilisé par <see cref="CombatTimeoutScheduler"/> pour vérifier le délai de choix.</summary>
    public IReadOnlyCollection<LootSession> All() => _sessions.Values.ToList();
}
