namespace Aetheria.Shared.Models;

/// <summary>Voir Docs/Idees.md — résultat d'une salle Événement : petit bonus or/XP instantané, plutôt qu'un buff porté sur le reste de l'étage (aucun état de progression d'étage n'est aujourd'hui suivi côté serveur entre deux salles, l'exploration en couloir est pilotée par le Client).</summary>
public sealed class EventRoomResult
{
    public required int Gold { get; init; }
    public required int Experience { get; init; }
}
