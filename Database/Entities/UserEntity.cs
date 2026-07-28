using Aetheria.Shared.Enums;

namespace Aetheria.Database.Entities;

/// <summary>Compte joueur (table <c>Users</c>). Un compte possède plusieurs <see cref="CharacterEntity"/>.</summary>
public sealed class UserEntity
{
    public Guid Id { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }

    /// <summary>Hash BCrypt du mot de passe — jamais le mot de passe en clair (voir Server/Networking).</summary>
    public required string PasswordHash { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsBanned { get; set; }
    public string? BanReason { get; set; }

    /// <summary>Compte administrateur (voir AdminPanel) — peut supprimer/modifier les comptes joueurs.</summary>
    public bool IsAdmin { get; set; }

    /// <summary>Grade communautaire affiché dans le tchat/la liste des joueurs en ligne (voir GDD — assignable par un administrateur).</summary>
    public UserRank Rank { get; set; } = UserRank.Joueur;

    /// <summary>
    /// Suppression douce : le compte est masqué (connexion refusée) mais conservé en base pour
    /// permettre une restauration réelle par un administrateur (voir <c>Docs/GameDesign.md</c> —
    /// "restaurer un compte").
    /// </summary>
    public bool IsDeleted { get; set; }

    public List<CharacterEntity> Characters { get; set; } = new();
}
