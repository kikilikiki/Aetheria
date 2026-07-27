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

    public List<CharacterEntity> Characters { get; set; } = new();
}
