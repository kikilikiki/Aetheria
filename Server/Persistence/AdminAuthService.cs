using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.Persistence;

/// <summary>
/// Vérifie qu'un jeton de session appartient bien à un compte administrateur (voir
/// <c>UserEntity.IsAdmin</c> et <c>AdminAccountSeeder</c>) avant d'autoriser une action
/// destructive de l'AdminPanel/Launcher (suppression/modification de compte joueur, grade,
/// bannissement, mute, ...). Un grade Fondateur (voir GDD/demande utilisateur — "réservé aux
/// admin/fondateur") donne aussi accès, sans nécessiter le flag technique <c>IsAdmin</c>.
/// </summary>
public static class AdminAuthService
{
    public static async Task<UserEntity> RequireAdminAsync(
        AetheriaDbContext db, SessionTokenStore tokenStore, string sessionToken, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !(user.IsAdmin || user.Rank == UserRank.Fondateur))
        {
            throw new AccountOperationException("Action réservée aux comptes administrateur/fondateur.");
        }

        return user;
    }
}
