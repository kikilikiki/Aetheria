using System.Collections.Concurrent;

namespace Aetheria.Server.World.Combat;

/// <summary>
/// Butins en cours de répartition, conservés en mémoire (même logique que
/// <see cref="CombatSessionStore"/>, y compris la rétention courte après résolution — voir
/// <see cref="LootSession.ResolvedAtUtc"/> — pour qu'un coéquipier qui sonde encore l'état après
/// coup le voie résolu plutôt qu'introuvable, voir GDD/demande utilisateur : "la première
/// personne à avoir fait le choix a connexion au serveur impossible").
/// </summary>
public sealed class LootSessionStore
{
    private static readonly TimeSpan ResolvedRetention = TimeSpan.FromMinutes(3);

    private readonly ConcurrentDictionary<Guid, LootSession> _sessions = new();

    public void Add(LootSession session)
    {
        _sessions[session.Id] = session;
        PruneResolved();
    }

    public bool TryGet(Guid id, out LootSession session) => _sessions.TryGetValue(id, out session!);

    public void Remove(Guid id) => _sessions.TryRemove(id, out _);

    /// <summary>Tous les butins en cours de répartition — utilisé par <see cref="CombatTimeoutScheduler"/> pour vérifier le délai de choix.</summary>
    public IReadOnlyCollection<LootSession> All() => _sessions.Values.ToList();

    /// <summary>Purge opportuniste (appelée à chaque nouveau butin) des butins résolus depuis plus de <see cref="ResolvedRetention"/>.</summary>
    private void PruneResolved()
    {
        var cutoff = DateTime.UtcNow - ResolvedRetention;
        foreach (var (id, session) in _sessions)
        {
            if (session.ResolvedAtUtc is { } resolvedAt && resolvedAt < cutoff)
            {
                _sessions.TryRemove(id, out _);
            }
        }
    }
}
