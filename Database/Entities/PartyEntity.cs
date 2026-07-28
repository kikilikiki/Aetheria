namespace Aetheria.Database.Entities;

/// <summary>
/// Groupe de joueurs (table <c>Parties</c>) — voir <c>Docs/GameDesign.md</c> : XP partagée entre
/// tous les membres, mobs sauvages hors donjon scalés sur le niveau du chef de groupe.
/// </summary>
public sealed class PartyEntity
{
    public Guid Id { get; set; }

    /// <summary>Code à 5 chiffres, à communiquer entre joueurs pour rejoindre le groupe (voir GDD/demande utilisateur — "les identifiants de groupe doivent être de 5 chiffres"), plus court/lisible qu'un GUID.</summary>
    public required string JoinCode { get; set; }

    public Guid LeaderCharacterId { get; set; }
    public CharacterEntity? LeaderCharacter { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
