namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/profile/update</c> — voir GDD/demande utilisateur "éditeur de profil".</summary>
public sealed class UpdateProfileRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public string Description { get; init; } = string.Empty;
    public int? ShowcaseItemId { get; init; }
    public string? ActiveTitle { get; init; }

    /// <summary>Voir GDD/demande utilisateur — "Collections : montures, ailes".</summary>
    public string? ActiveMountKey { get; init; }
    public string? ActiveWingKey { get; init; }
}
