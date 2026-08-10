using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.Discord;

/// <summary>
/// Lien compte Aetheria &lt;-&gt; compte Discord (voir GDD/demande utilisateur — "système de link
/// le compte discord avec le jeu"). Flux en deux temps, pensé pour ne jamais faire confiance à un
/// identifiant Discord fourni tel quel par le joueur (usurpation) :
/// 1. En jeu, la commande <c>/discord</c> (voir <see cref="Aetheria.Server.Networking.PlayerSession"/>)
///    génère un code court à usage unique, propre au compte connecté.
/// 2. Sur Discord, la commande <c>/link &lt;code&gt;</c> (voir <see cref="DiscordGatewayClient"/>)
///    fournit ce code : le compte Discord qui l'utilise (identité garantie par Discord lui-même,
///    pas par une saisie libre) est alors lié au compte propriétaire du code.
/// </summary>
public static class DiscordLinkService
{
    private const int CodeLength = 6;
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // sans caractères ambigus (0/O, 1/I/L)

    /// <summary>Génère un nouveau code, l'enregistre sur le compte (remplace un éventuel code précédent) et le renvoie.</summary>
    public static string GenerateLinkCode(UserEntity user)
    {
        var code = string.Concat(Enumerable.Range(0, CodeLength).Select(_ => CodeAlphabet[Random.Shared.Next(CodeAlphabet.Length)]));
        user.PendingDiscordLinkCode = code;
        user.PendingDiscordLinkCodeExpiresUtc = DateTime.UtcNow.Add(CodeLifetime);
        return code;
    }

    public enum LinkResult
    {
        Success,
        InvalidOrExpiredCode,
    }

    /// <summary>
    /// Consomme un code saisi côté Discord : si valide, lie <paramref name="discordUserId"/> au
    /// compte propriétaire du code (en retirant d'abord ce même identifiant Discord de tout autre
    /// compte auquel il serait déjà lié, pour permettre un re-link après changement de compte).
    /// </summary>
    public static async Task<(LinkResult Result, UserEntity? User)> TryLinkAsync(AetheriaDbContext db, string code, string discordUserId, CancellationToken ct = default)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();

        var user = await db.Users.FirstOrDefaultAsync(
            u => u.PendingDiscordLinkCode == normalizedCode && !u.IsDeleted, ct);

        if (user is null || user.PendingDiscordLinkCodeExpiresUtc is null || user.PendingDiscordLinkCodeExpiresUtc < DateTime.UtcNow)
        {
            return (LinkResult.InvalidOrExpiredCode, null);
        }

        var previousOwner = await db.Users.FirstOrDefaultAsync(u => u.DiscordUserId == discordUserId && u.Id != user.Id, ct);
        if (previousOwner is not null)
        {
            previousOwner.DiscordUserId = null;
        }

        user.DiscordUserId = discordUserId;
        user.PendingDiscordLinkCode = null;
        user.PendingDiscordLinkCodeExpiresUtc = null;

        await db.SaveChangesAsync(ct);
        return (LinkResult.Success, user);
    }
}
