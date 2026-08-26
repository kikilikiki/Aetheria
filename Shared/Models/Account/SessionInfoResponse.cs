using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models.Account;

/// <summary>
/// Réponse JSON réussie de <c>GET /api/account/session</c> — revalidation légère d'un jeton de
/// session persisté (voir GDD/demande utilisateur — "rester connecté jusqu'à la déconnexion"),
/// sans redemander les identifiants ni relire toute la liste des personnages comme le ferait un
/// nouveau login.
/// </summary>
public sealed class SessionInfoResponse
{
    public required Guid UserId { get; init; }
    public required bool IsAdmin { get; init; }
    public required UserRank Rank { get; init; }

    /// <summary>Voir Docs/Idees.md — vraie image de profil, <c>null</c> tant qu'aucune image n'a été envoyée.</summary>
    public string? AvatarUrl { get; init; }
}
