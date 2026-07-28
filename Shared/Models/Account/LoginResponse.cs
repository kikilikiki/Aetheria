namespace Aetheria.Shared.Models.Account;

/// <summary>Réponse JSON réussie de <c>POST /api/account/login</c>.</summary>
public sealed class LoginResponse
{
    public required string SessionToken { get; init; }
    public required Guid UserId { get; init; }
    public required IReadOnlyList<CharacterSummary> Characters { get; init; }
}

/// <summary>
/// Résumé d'un personnage affiché dans l'écran de sélection (en jeu, voir GDD — la création/
/// sélection ne se fait plus dans le Launcher).
/// </summary>
public sealed class CharacterSummary
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required int Level { get; init; }
    public int SkinColorIndex { get; init; }
    public int HairStyleIndex { get; init; }
    public int HairColorIndex { get; init; }
    public int ClothesColorIndex { get; init; }
    public int AccessoryIndex { get; init; }
}
