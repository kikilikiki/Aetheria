using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Aetheria.Server.Persistence;

/// <summary>
/// Jetons de session en mémoire : émis à la connexion (voir <see cref="AccountService"/>),
/// validés par le serveur de jeu TCP quand le client demande à entrer dans le monde
/// (<c>EnterWorldRequestPacket</c>). Un jeton est un secret aléatoire de 256 bits — pas un
/// JWT — car il n'a besoin d'être compris que par ce même serveur.
/// </summary>
public sealed class SessionTokenStore
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(12);

    private readonly ConcurrentDictionary<string, Session> _sessions = new();

    public string CreateToken(Guid userId)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _sessions[token] = new Session(userId, DateTime.UtcNow + TokenLifetime);
        return token;
    }

    public bool TryValidate(string token, out Guid userId)
    {
        if (_sessions.TryGetValue(token, out var session) && session.ExpiresAtUtc > DateTime.UtcNow)
        {
            userId = session.UserId;
            return true;
        }

        userId = default;
        return false;
    }

    private readonly record struct Session(Guid UserId, DateTime ExpiresAtUtc);
}
