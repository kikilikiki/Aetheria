using System.Net.Mail;
using System.Security.Claims;
using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Shared.Enums;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Web.Services;

/// <summary>
/// Inscription / connexion pour le portail web. Autonome (aucune dépendance sur Aetheria.Server) :
/// applique les mêmes règles que <c>Server/Persistence/AccountService.cs</c> et le même hachage
/// BCrypt que <c>Server/Persistence/PasswordHasher.cs</c> (work factor 12) contre la table
/// <c>Users</c> partagée. Un compte créé ici est utilisable en jeu, et réciproquement.
/// </summary>
public sealed class WebAccountService(AetheriaDbContext db)
{
    /// <summary>Doit rester identique à <c>Server/Persistence/PasswordHasher.WorkFactor</c>.</summary>
    private const int BcryptWorkFactor = 12;

    public sealed record Result(bool Success, string? Error, UserEntity? User)
    {
        public static Result Fail(string error) => new(false, error, null);
        public static Result Ok(UserEntity user) => new(true, null, user);
    }

    public async Task<Result> RegisterAsync(string username, string email, string password, CancellationToken ct = default)
    {
        username = username.Trim();
        email = email.Trim();

        if (username.Length is < 3 or > 20)
        {
            return Result.Fail("Le pseudo doit faire entre 3 et 20 caractères.");
        }

        if (!IsValidEmail(email))
        {
            return Result.Fail("L'email doit être au format exemple@domaine.com.");
        }

        if (password.Length < 6)
        {
            return Result.Fail("Le mot de passe doit faire au moins 6 caractères.");
        }

        if (await db.Users.AnyAsync(u => u.Username == username, ct))
        {
            return Result.Fail("Ce pseudo est déjà utilisé.");
        }

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            return Result.Fail("Cet email est déjà utilisé.");
        }

        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, BcryptWorkFactor),
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return Result.Ok(user);
    }

    public async Task<Result> VerifyAsync(string usernameOrEmail, string password, CancellationToken ct = default)
    {
        usernameOrEmail = usernameOrEmail.Trim();

        var user = await db.Users.FirstOrDefaultAsync(
            u => u.Username == usernameOrEmail || u.Email == usernameOrEmail, ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return Result.Fail("Identifiants invalides.");
        }

        if (user.IsDeleted)
        {
            return Result.Fail("Ce compte a été supprimé.");
        }

        if (user.IsBanned)
        {
            return Result.Fail($"Compte banni : {user.BanReason ?? "aucune raison fournie"}.");
        }

        return Result.Ok(user);
    }

    /// <summary>Construit le principal (cookie d'authentification) à partir d'un compte vérifié.</summary>
    public static ClaimsPrincipal BuildPrincipal(UserEntity user)
    {
        var isStaff = user.IsAdmin || user.Rank == UserRank.Fondateur;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Rank.ToString()),
            new("is_admin", user.IsAdmin ? "true" : "false"),
            new("is_staff", isStaff ? "true" : "false"),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var address = new MailAddress(email);
            return address.Address == email && email.Contains('@');
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
