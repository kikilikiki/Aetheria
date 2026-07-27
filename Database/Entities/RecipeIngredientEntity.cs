namespace Aetheria.Database.Entities;

/// <summary>Une ligne d'ingrédient requis par une <see cref="RecipeEntity"/>.</summary>
public sealed class RecipeIngredientEntity
{
    public Guid Id { get; set; }

    public int RecipeId { get; set; }
    public RecipeEntity? Recipe { get; set; }

    public int ItemId { get; set; }
    public ItemEntity? Item { get; set; }

    public int Quantity { get; set; } = 1;
}
