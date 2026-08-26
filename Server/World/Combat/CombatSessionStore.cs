using System.Collections.Concurrent;

namespace Aetheria.Server.World.Combat;

/// <summary>
/// Combats en cours, conservés en mémoire (comme <see cref="Aetheria.Server.Persistence.SessionTokenStore"/>) :
/// un combat est un état transitoire, il n'a pas sa place en base de données. Un combat terminé
/// (<see cref="CombatSession.FinishedAtUtc"/> renseigné) n'est plus retiré immédiatement (voir
/// GDD/demande utilisateur — "le joueur qui n'a pas donné le coup final est bloqué, ça ne
/// fonctionne pas") : un coéquipier dont le client sonde encore l'état après la fin du combat
/// (voir <c>CombatService.TryGetState</c>) doit pouvoir le lire comme terminé — pas comme
/// introuvable — pour afficher son propre butin/écran de fin. Purgé après un court délai
/// (<see cref="FinishedRetention"/>) plutôt que de garder les sessions terminées indéfiniment.
/// </summary>
public sealed class CombatSessionStore
{
    private static readonly TimeSpan FinishedRetention = TimeSpan.FromMinutes(3);

    private readonly ConcurrentDictionary<Guid, CombatSession> _sessions = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _partyCreationLocks = new();

    /// <summary>Voir Docs/Idees.md — verrou anti-double-création de combat de groupe : un seul <see cref="SemaphoreSlim"/> par groupe, créé paresseusement, jamais retiré (volume trop faible pour justifier un nettoyage).</summary>
    public SemaphoreSlim GetPartyCreationLock(Guid partyId) => _partyCreationLocks.GetOrAdd(partyId, _ => new SemaphoreSlim(1, 1));

    public void Add(CombatSession session)
    {
        _sessions[session.Id] = session;
        PruneFinished();
    }

    public bool TryGet(Guid id, out CombatSession session) => _sessions.TryGetValue(id, out session!);

    public void Remove(Guid id) => _sessions.TryRemove(id, out _);

    /// <summary>Tous les combats connus (terminés ou non) — utilisé par <see cref="CombatTimeoutScheduler"/> pour vérifier les délais de tour.</summary>
    public IReadOnlyCollection<CombatSession> All() => _sessions.Values.ToList();

    /// <summary>Combat de groupe déjà en cours pour ce groupe, s'il y en a un (voir GDD/demande utilisateur — combat partagé entre membres d'un groupe).</summary>
    public bool TryGetActiveByPartyId(Guid partyId, out CombatSession session)
    {
        session = _sessions.Values.FirstOrDefault(s => s.PartyId == partyId && !s.IsFinished)!;
        return session is not null;
    }

    /// <summary>Purge opportuniste (appelée à chaque nouveau combat) des combats terminés depuis plus de <see cref="FinishedRetention"/> — évite une fuite mémoire sans nécessiter de tâche d'arrière-plan dédiée pour un volume aussi faible.</summary>
    private void PruneFinished()
    {
        var cutoff = DateTime.UtcNow - FinishedRetention;
        foreach (var (id, session) in _sessions)
        {
            if (session.FinishedAtUtc is { } finishedAt && finishedAt < cutoff)
            {
                _sessions.TryRemove(id, out _);
            }
        }
    }
}
