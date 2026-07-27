namespace Aetheria.Database.Entities;

/// <summary>
/// Avancement d'un personnage sur une quête. Table de liaison nécessaire pour que le
/// catalogue <c>Quests</c> serve à quelque chose (sans elle, aucune progression n'est
/// trackée par joueur).
/// </summary>
public sealed class CharacterQuestProgressEntity
{
    public Guid Id { get; set; }

    public Guid CharacterId { get; set; }
    public CharacterEntity? Character { get; set; }

    public int QuestId { get; set; }
    public QuestEntity? Quest { get; set; }

    public bool IsCompleted { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
