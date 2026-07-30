namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/monsters/fuse/claim</c> et <c>POST /api/monsters/breed/claim</c> — voir GDD/demande utilisateur "ajoute un temps et une validation avant de le faire".</summary>
public sealed class ClaimPendingMonsterRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
}
