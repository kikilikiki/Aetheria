namespace Aetheria.Database.Entities;

/// <summary>Appartenance d'un personnage à un groupe (table <c>PartyMembers</c>).</summary>
public sealed class PartyMemberEntity
{
    public Guid Id { get; set; }

    public Guid PartyId { get; set; }
    public PartyEntity? Party { get; set; }

    public Guid CharacterId { get; set; }
    public CharacterEntity? Character { get; set; }

    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
}
