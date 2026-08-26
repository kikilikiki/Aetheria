using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models.Admin;

/// <summary>Vue joueur pour l'AdminPanel (voir <c>Docs/GameDesign.md</c> — section AdminPanel).</summary>
public sealed class AdminUserSummary
{
    public required Guid Id { get; init; }
    public required string Username { get; init; }
    public required string Email { get; init; }
    public required bool IsBanned { get; init; }
    public string? BanReason { get; init; }
    public required bool IsAdmin { get; init; }
    public required bool IsDeleted { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required int CharacterCount { get; init; }
    public required UserRank Rank { get; init; }

    /// <summary>Voir GDD/demande utilisateur — "mute pour ne pas qu'il parle dans le tchat".</summary>
    public required bool IsMuted { get; init; }

    /// <summary>Dernière IP connue (voir GDD/demande utilisateur — "ban ip") — <c>null</c> si le compte ne s'est jamais connecté depuis ce changement.</summary>
    public string? LastKnownIp { get; init; }

    /// <summary>Voir GDD/demande utilisateur — "la couleur a gauche du pseudo [dans le Launcher] correspond a si la personne est en ligne ou pas" : vrai si AU MOINS un personnage de ce compte a une session active (voir Server.Networking.WorldSessionRegistry.IsOnline), pas une notion de compte connecté à la Boutique/API séparément.</summary>
    public required bool IsOnline { get; init; }

    /// <summary>Voir Docs/Idees.md — vraie image de profil, <c>null</c> tant qu'aucune image n'a été envoyée (fallback pastille/initiale).</summary>
    public string? AvatarUrl { get; init; }
}
