using Aetheria.Shared.Enums;

namespace Aetheria.Database.Entities;

/// <summary>
/// Voir Docs/Idees.md — historique de tchat persisté entre connexions (jusqu'ici borné à ~100
/// lignes en mémoire côté client uniquement, perdu à chaque reconnexion). Ne couvre que les
/// canaux Global et Guilde (les deux onglets réels du panneau Tchat, voir GDD/demande
/// utilisateur) — pas les messages privés, qui n'ont pas de vue "historique" dans le Client
/// aujourd'hui. <see cref="GuildId"/> est <c>null</c> pour un message Global, renseigné pour un
/// message de Guilde (filtre l'historique par guilde plutôt que de le mélanger entre guildes).
/// </summary>
public sealed class ChatMessageEntity
{
    public Guid Id { get; set; }
    public ChatChannel Channel { get; set; }
    public Guid? GuildId { get; set; }
    public Guid SenderCharacterId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public UserRank SenderRank { get; set; } = UserRank.Joueur;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
