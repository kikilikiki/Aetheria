namespace Aetheria.Database.Entities;

/// <summary>
/// Un personnage en a bloqué un autre (table <c>Blocks</c>) — voir demande utilisateur
/// (« bloquer » depuis le menu d'interaction en jeu). Blocage « complet » : le bloqueur ne voit
/// plus les messages de tchat du bloqué, et <b>ni l'un ni l'autre</b> ne peut envoyer à l'autre
/// une demande d'ami ou un défi en duel (voir <c>BlockService.AreBlockedEitherWay</c>). Les deux
/// restent visibles sur la carte.
/// </summary>
public sealed class BlockEntity
{
    public Guid Id { get; set; }

    public Guid BlockerCharacterId { get; set; }
    public CharacterEntity? BlockerCharacter { get; set; }

    public Guid BlockedCharacterId { get; set; }
    public CharacterEntity? BlockedCharacter { get; set; }

    /// <summary>Pseudo du bloqué au moment du blocage (affichage sans jointure).</summary>
    public string BlockedCharacterName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
