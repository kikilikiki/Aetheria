namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/game/give-mount</c> — voir GDD/demande utilisateur "ajoute une commande pour give des montures".</summary>
public sealed class AdminGiveMountRequest
{
    public required string SessionToken { get; init; }
    public required string TargetCharacterName { get; init; }
    public required string MountKey { get; init; }
}
