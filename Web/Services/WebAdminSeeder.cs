using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Web.Services;

/// <summary>
/// Amorce un compte administrateur si la base ne contient encore aucun compte <c>IsAdmin</c>
/// (cas d'une base Neon vierge démarrée par le portail web avant le serveur de jeu). Le mot de
/// passe n'est jamais codé en dur ici (contrairement à <c>Server/Persistence/AdminAccountSeeder.cs</c>,
/// gitignoré) : il vient de la variable d'environnement <c>AETHERIA_ADMIN_BOOTSTRAP_PASSWORD</c>.
/// Absente ⇒ aucun compte n'est créé (le serveur de jeu s'en chargera de son côté sur la même base).
/// </summary>
public static class WebAdminSeeder
{
    public const string DefaultUsername = "admin";
    public const string DefaultEmail = "admin@aetheria.local";

    public static async Task SeedAsync(AetheriaDbContext db, ILogger? logger = null, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(u => u.IsAdmin, ct))
        {
            return;
        }

        var password = Environment.GetEnvironmentVariable("AETHERIA_ADMIN_BOOTSTRAP_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            logger?.LogWarning(
                "Aucun compte administrateur en base et AETHERIA_ADMIN_BOOTSTRAP_PASSWORD non défini : "
                + "l'administration du site sera indisponible tant que le serveur de jeu n'aura pas amorcé le compte admin.");
            return;
        }

        db.Users.Add(new UserEntity
        {
            Id = Guid.NewGuid(),
            Username = Environment.GetEnvironmentVariable("AETHERIA_ADMIN_BOOTSTRAP_USERNAME") ?? DefaultUsername,
            Email = Environment.GetEnvironmentVariable("AETHERIA_ADMIN_BOOTSTRAP_EMAIL") ?? DefaultEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12),
            IsAdmin = true,
        });

        await db.SaveChangesAsync(ct);
        logger?.LogInformation("Compte administrateur amorcé (AETHERIA_ADMIN_BOOTSTRAP_PASSWORD).");
    }
}
