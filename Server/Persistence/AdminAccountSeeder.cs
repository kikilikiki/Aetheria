using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.Persistence;

/// <summary>
/// Crée le compte administrateur par défaut au premier démarrage (voir <c>Docs/README.md</c> —
/// section AdminPanel). Mot de passe volontairement simple : ce projet n'a pas de déploiement
/// public, mais un vrai déploiement DOIT changer ce mot de passe immédiatement.
/// </summary>
public static class AdminAccountSeeder
{
    public const string DefaultUsername = "admin";
    public const string DefaultEmail = "admin@aetheria.local";
    public const string DefaultPassword = "ChangeMoi123!";

    public static async Task SeedAsync(AetheriaDbContext db, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(u => u.IsAdmin, ct))
        {
            return;
        }

        db.Users.Add(new UserEntity
        {
            Id = Guid.NewGuid(),
            Username = DefaultUsername,
            Email = DefaultEmail,
            PasswordHash = PasswordHasher.Hash(DefaultPassword),
            IsAdmin = true,
        });

        await db.SaveChangesAsync(ct);
    }
}
