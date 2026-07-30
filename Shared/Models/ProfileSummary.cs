using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>Voir GDD/demande utilisateur — "un endroit pour modifier son profil (description, item à montrer, titre, grade)" : réponse de <c>GET /api/profile/{characterId}</c>, consultable pour n'importe quel personnage (soi ou un ami/adversaire).</summary>
public sealed class ProfileSummary
{
    public required string CharacterName { get; init; }
    public required string Description { get; init; }
    public int Level { get; init; }
    public UserRank Rank { get; init; }
    public int? ShowcaseItemId { get; init; }
    public string? ShowcaseItemName { get; init; }
    public string? ActiveTitle { get; init; }
    public IReadOnlyList<string> OwnedTitles { get; init; } = [];

    /// <summary>Voir GDD/demande utilisateur — "Collections : montures, ailes".</summary>
    public string? ActiveMountKey { get; init; }
    public IReadOnlyList<string> OwnedMountKeys { get; init; } = [];
    public string? ActiveWingKey { get; init; }
    public IReadOnlyList<string> OwnedWingKeys { get; init; } = [];
}
