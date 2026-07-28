using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models.Account;

/// <summary>Réponse JSON réussie de <c>POST /api/account/login</c>.</summary>
public sealed class LoginResponse
{
    public required string SessionToken { get; init; }
    public required Guid UserId { get; init; }
    public required IReadOnlyList<CharacterSummary> Characters { get; init; }

    /// <summary>Permission technique admin (voir GDD/demande utilisateur — panneau d'administration accessible depuis le Launcher pour les comptes admin/fondateur).</summary>
    public required bool IsAdmin { get; init; }

    /// <summary>Grade communautaire (voir GDD — affiché en jeu, détermine aussi l'accès au panneau d'administration du Launcher pour Fondateur).</summary>
    public required UserRank Rank { get; init; }
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
    public required KingdomType Kingdom { get; init; }
    public int SkinColorIndex { get; init; }
    public int HairStyleIndex { get; init; }
    public int HairColorIndex { get; init; }
    public int ClothesColorIndex { get; init; }
    public int AccessoryIndex { get; init; }
}
