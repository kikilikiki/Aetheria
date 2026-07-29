namespace Aetheria.Database.Entities;

/// <summary>
/// Titre débloqué par un personnage (table <c>CharacterTitles</c>) — voir GDD/demande
/// utilisateur "des titres que l'on peut obtenir en pvp dans des classements". Accumulé
/// définitivement une fois débloqué (voir <see cref="Server.World.TitleCatalog"/>) : un titre
/// obtenu en dépassant un palier ELO reste possédé même si le classement redescend ensuite.
/// </summary>
public sealed class CharacterTitleEntity
{
    public Guid Id { get; set; }

    public Guid CharacterId { get; set; }
    public CharacterEntity? Character { get; set; }

    public required string TitleKey { get; set; }
    public DateTime EarnedAtUtc { get; set; } = DateTime.UtcNow;
}
