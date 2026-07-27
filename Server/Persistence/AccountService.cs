using Aetheria.Database.Context;
using Aetheria.Database.Entities;
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

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await db.Users
            .Include(u => u.Characters)
            .FirstOrDefaultAsync(
                u => u.Username == request.UsernameOrEmail || u.Email == request.UsernameOrEmail, ct);

        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new AccountOperationException("Identifiants invalides.");
        }

        if (user.IsBanned)
        {
            throw new AccountOperationException($"Compte banni : {user.BanReason ?? "aucune raison fournie"}.");
        }

        var token = tokenStore.CreateToken(user.Id);

        return new LoginResponse
        {
            SessionToken = token,
            UserId = user.Id,
            Characters = user.Characters
                .Select(c => new CharacterSummary { Id = c.Id, Name = c.Name, Level = c.Level })
                .ToList(),
        };
    }
}
