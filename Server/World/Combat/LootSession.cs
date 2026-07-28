using Aetheria.Shared.Models.Combat;

namespace Aetheria.Server.World.Combat;

/// <summary>État en mémoire d'un butin en cours de répartition (voir <c>LootService</c>).</summary>
public sealed class LootSession
{
    public required Guid Id { get; init; }
    public required IReadOnlyList<LootItemEntry> Items { get; init; }
    public required IReadOnlyList<Guid> EligibleCharacterIds { get; init; }

    /// <summary>Réclamation en cours : personnage -> index de l'objet visé (une réclamation par personnage, remplaçable tant que non résolu).</summary>
    public Dictionary<Guid, int> Claims { get; } = [];

    public bool IsResolved { get; set; }

    /// <summary>Index de l'objet -> personnage gagnant, renseigné uniquement après résolution.</summary>
    public IReadOnlyDictionary<int, Guid>? Winners { get; set; }

    /// <summary>Voir GDD/demande utilisateur — "timer de 10 secondes pour le choix des gains" (<see cref="Aetheria.Shared.GameInfo.LootChoiceTimeoutSeconds"/>), vérifié par <c>CombatTimeoutScheduler</c>.</summary>
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Renseigné dès la résolution (voir <see cref="LootSessionStore"/>) : la session n'est plus
    /// retirée immédiatement du store pour que tout coéquipier qui sonde encore l'état après
    /// résolution (voir GDD/demande utilisateur — "la première personne à avoir fait le choix a
    /// 'connexion au serveur impossible'") la voie bien résolue au lieu d'un butin introuvable
    /// (404), avant d'être purgée après un court délai.
    /// </summary>
    public DateTime? ResolvedAtUtc { get; set; }
}
