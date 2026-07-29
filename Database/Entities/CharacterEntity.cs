using Aetheria.Shared.Enums;

namespace Aetheria.Database.Entities;

/// <summary>Personnage jouable (table <c>Characters</c>), appartenant à un <see cref="UserEntity"/>.</summary>
public sealed class CharacterEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }

    public required string Name { get; set; }
    public CharacterClass Class { get; set; }
    public KingdomType Kingdom { get; set; }

    public int Level { get; set; } = 1;
    public long Experience { get; set; }
    public long Gold { get; set; }

    // Apparence (voir GDD — création de personnage en jeu) : indices dans de petites palettes
    // fixes côté Client plutôt que des couleurs libres, pour rester cohérent avec le rendu par
    // quads colorés du moteur (pas de sprite/texture de personnage — voir Docs/README.md).
    public int SkinColorIndex { get; set; }
    public int HairStyleIndex { get; set; }
    public int HairColorIndex { get; set; }
    public int ClothesColorIndex { get; set; }
    public int AccessoryIndex { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Voir GDD/demande utilisateur — "un endroit pour modifier son profil (description, item que
    // l'on veut montrer, titre, grade)" : le grade affiché est UserEntity.Rank (lecture seule,
    // pas dupliqué ici) ; ces trois champs sont les seuls réellement éditables par le joueur.
    public string ProfileDescription { get; set; } = string.Empty;
    public int? ShowcaseItemId { get; set; }

    /// <summary>Voir GDD/demande utilisateur — "des titres que l'on peut obtenir en pvp dans des classements" : doit correspondre à un <see cref="CharacterTitleEntity.TitleKey"/> déjà possédé (voir ProfileService.UpdateAsync), sinon ignoré.</summary>
    public string? ActiveTitle { get; set; }

    public List<MonsterEntity> Monsters { get; set; } = new();
    public List<InventoryItemEntity> InventoryItems { get; set; } = new();
    public StatisticsEntity? Statistics { get; set; }
    public List<CharacterTitleEntity> Titles { get; set; } = new();
}
