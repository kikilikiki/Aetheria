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
}
