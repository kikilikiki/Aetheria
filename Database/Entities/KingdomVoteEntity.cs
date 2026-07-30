namespace Aetheria.Database.Entities;

/// <summary>Un vote pour l'élection du roi d'un royaume (voir GDD/demande utilisateur — "élections du roi") : un vote par personnage électeur, remplacé en cas de revote.</summary>
public sealed class KingdomVoteEntity
{
    public Guid Id { get; set; }

    public int KingdomId { get; set; }
    public KingdomEntity? Kingdom { get; set; }

    public Guid VoterCharacterId { get; set; }
    public Guid CandidateCharacterId { get; set; }
}
