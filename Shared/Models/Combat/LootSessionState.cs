namespace Aetheria.Shared.Models.Combat;

/// <summary>
/// État d'un butin de fin de combat (voir GDD — 4 objets à départager, tirage aléatoire en cas
/// d'égalité). <see cref="Winners"/> n'est renseigné qu'une fois <see cref="IsResolved"/> vrai.
/// </summary>
public sealed class LootSessionState
{
    public required Guid LootId { get; init; }
    public required IReadOnlyList<LootItemEntry> Items { get; init; }
    public required IReadOnlyList<Guid> EligibleCharacterIds { get; init; }
    public required IReadOnlyList<Guid> ClaimedCharacterIds { get; init; }
    public required bool IsResolved { get; init; }
    public IReadOnlyDictionary<int, Guid>? Winners { get; init; }

    /// <summary>
    /// Nombre de joueurs ayant actuellement choisi chaque objet (index de l'objet -> nombre de
    /// réclamations), pour l'afficher clairement pendant la répartition (voir GDD/demande
    /// utilisateur — "afficher une petite icône pour dire choisi par un joueur, ajouter en 2 si
    /// ils sont 2 ainsi de suite"). Absent des index sans réclamation.
    /// </summary>
    public required IReadOnlyDictionary<int, int> ClaimCountsByItemIndex { get; init; }

    /// <summary>Voir GDD/demande utilisateur — "timer de 10 secondes pour le choix des gains" : le client affiche un compte à rebours à partir de cette valeur (<see cref="Aetheria.Shared.GameInfo.LootChoiceTimeoutSeconds"/>).</summary>
    public required DateTime CreatedAtUtc { get; init; }
}
