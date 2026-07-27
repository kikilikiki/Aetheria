using Aetheria.Shared.Enums;

namespace Aetheria.Database.Entities;

/// <summary>
/// Recette d'artisanat (table <c>Recipes</c>) : consomme des <see cref="RecipeIngredientEntity"/>
/// pour produire un objet, si le métier requis est assez haut niveau (voir GDD — chaîne
/// Mineur → Minerai → Forgeron → Arme → Enchantement → Vente Hôtel des ventes).
/// </summary>
public sealed class RecipeEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public ProfessionType Profession { get; set; }
    public int RequiredLevel { get; set; } = 1;

    public int ResultItemId { get; set; }
    public ItemEntity? ResultItem { get; set; }
    public int ResultQuantity { get; set; } = 1;

    public List<RecipeIngredientEntity> Ingredients { get; set; } = new();
}
