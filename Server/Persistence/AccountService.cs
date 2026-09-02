using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models.Account;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.Persistence;

/// <summary>
/// Logique d'inscription/connexion (voir <c>Docs/GameDesign.md</c> — Système de compte).
/// Consommée par l'API HTTP exposée dans <c>Program.cs</c> ; ne connaît rien du transport
/// (HTTP, TCP) pour rester testable indépendamment.
/// </summary>
public sealed class AccountService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    public async Task<Guid> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (!IsValidEmail(request.Email))
        {
            throw new AccountOperationException("L'email doit être au format exemple@domaine.com.");
        }

        var usernameTaken = await db.Users.AnyAsync(u => u.Username == request.Username, ct);
        if (usernameTaken)
        {
            throw new AccountOperationException("Ce pseudo est déjà utilisé.");
        }

        var emailTaken = await db.Users.AnyAsync(u => u.Email == request.Email, ct);
        if (emailTaken)
        {
            throw new AccountOperationException("Cet email est déjà utilisé.");
        }

        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = PasswordHasher.Hash(request.Password),
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return user.Id;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, string? remoteIp = null, CancellationToken ct = default)
    {
        // Voir GDD/demande utilisateur — "ban ip" : bloque la connexion depuis une IP bannie
        // quel que soit le compte utilisé, avant même de vérifier les identifiants.
        if (!string.IsNullOrWhiteSpace(remoteIp) && await db.BannedIps.AnyAsync(b => b.IpAddress == remoteIp, ct))
        {
            throw new AccountOperationException("Cette adresse IP est bannie.");
        }

        var user = await db.Users
            .Include(u => u.Characters)
            .FirstOrDefaultAsync(
                u => u.Username == request.UsernameOrEmail || u.Email == request.UsernameOrEmail, ct);

        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new AccountOperationException("Identifiants invalides.");
        }

        if (user.IsDeleted)
        {
            throw new AccountOperationException("Ce compte a été supprimé.");
        }

        if (user.IsBanned)
        {
            throw new AccountOperationException($"Compte banni : {user.BanReason ?? "aucune raison fournie"}.");
        }

        // Voir demande utilisateur — bêta fermée : les grades Joueur / VIP ne peuvent pas se
        // connecter, seuls Testeur / Ami / Modérateur / Fondateur et les comptes admin le peuvent.
        if (!BetaAccessPolicy.CanConnect(user.IsAdmin, user.Rank))
        {
            throw new AccountOperationException(BetaAccessPolicy.DeniedMessage);
        }

        // Voir GDD/demande utilisateur — "laisse allumé le serveur de prod et allume aussi le
        // serveur de dev mais seul moi peut accéder au serveur de dev et les autres accès à la
        // prod" : quand cette variable d'environnement est activée (mise en place seulement sur
        // l'instance dev, jamais sur la prod), seuls les comptes admin/Fondateur peuvent s'y
        // connecter — tout le monde d'autre est redirigé implicitement vers la prod.
        if (string.Equals(Environment.GetEnvironmentVariable("AETHERIA_RESTRICT_ACCESS"), "true", StringComparison.OrdinalIgnoreCase)
            && !user.IsAdmin && user.Rank != UserRank.Fondateur)
        {
            throw new AccountOperationException("Ce serveur est réservé à l'administrateur. Utilisez le serveur habituel.");
        }

        if (!string.IsNullOrWhiteSpace(remoteIp))
        {
            user.LastKnownIp = remoteIp;
        }

        var token = tokenStore.CreateToken(user.Id);
        await db.SaveChangesAsync(ct);

        return new LoginResponse
        {
            SessionToken = token,
            UserId = user.Id,
            IsAdmin = user.IsAdmin,
            Rank = user.Rank,
            AvatarUrl = user.AvatarUrl,
            Characters = user.Characters
                .Select(c => new CharacterSummary
                {
                    Id = c.Id,
                    Name = c.Name,
                    Level = c.Level,
                    Kingdom = c.Kingdom,
                    SkinColorIndex = c.SkinColorIndex,
                    HairStyleIndex = c.HairStyleIndex,
                    HairColorIndex = c.HairColorIndex,
                    ClothesColorIndex = c.ClothesColorIndex,
                    AccessoryIndex = c.AccessoryIndex,
                    HasSeenTutorial = c.HasSeenTutorial,
                })
                .ToList(),
        };
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var address = new System.Net.Mail.MailAddress(email);
            return address.Address == email && email.Contains('@');
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
