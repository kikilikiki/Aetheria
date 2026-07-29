using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>
/// Vue client d'une recette (voir GDD/demande utilisateur — "liste des items que l'on peut craft
/// et ce qu'il faut"). Reflète la forme JSON renvoyée par <c>GET /api/professions/recipes</c>
/// (qui sérialise l'entité EF Core telle quelle) : mêmes noms de propriétés, types différents
/// pour <c>ResultItem</c>/<c>Ingredients[].Item</c> (pas de dépendance du Client vers Database).
/// </summary>
public sealed class RecipeSummary
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public ProfessionType Profession { get; init; }
    public int RequiredLevel { get; init; } = 1;
    public int ResultItemId { get; init; }
    public RecipeItemRef? ResultItem { get; init; }
    public int ResultQuantity { get; init; } = 1;
    public List<RecipeIngredientSummary> Ingredients { get; init; } = [];
}

public sealed class RecipeIngredientSummary
{
    public int ItemId { get; init; }
    public RecipeItemRef? Item { get; init; }
    public int Quantity { get; init; } = 1;
}

public sealed class RecipeItemRef
{
    public string Name { get; init; } = string.Empty;
}
