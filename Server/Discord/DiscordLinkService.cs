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
    private const int CodeLength = 5;
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

        /// <summary>Le compte Aetheria propriétaire du code est déjà lié à un compte Discord (voir demande utilisateur — "un utilisateur ne peut se vérifier plus de 1 fois").</summary>
        AccountAlreadyLinked,

        /// <summary>Ce compte Discord est déjà lié à un autre compte Aetheria — une vérification n'engage qu'un seul compte de chaque côté.</summary>
        DiscordAccountAlreadyLinked,
    }

    /// <summary>
    /// Consomme un code saisi côté Discord : si valide et qu'aucun des deux comptes n'est déjà
    /// lié, lie <paramref name="discordUserId"/> au compte propriétaire du code. Voir demande
    /// utilisateur — "un utilisateur ne peut se vérifier plus de 1 fois", "les vérif se font pas
    /// par personnage mais par compte" : un lien est définitif une fois établi, dans les deux
    /// sens (un compte Aetheria ne peut avoir qu'un seul compte Discord lié, et réciproquement),
    /// contrairement à une première version qui permettait de voler silencieusement le lien d'un
    /// compte à un autre.
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

        if (user.DiscordUserId is { Length: > 0 })
        {
            return (LinkResult.AccountAlreadyLinked, null);
        }

        var alreadyLinkedTo = await db.Users.FirstOrDefaultAsync(u => u.DiscordUserId == discordUserId, ct);
        if (alreadyLinkedTo is not null)
        {
            return (LinkResult.DiscordAccountAlreadyLinked, null);
        }

        user.DiscordUserId = discordUserId;
        user.PendingDiscordLinkCode = null;
        user.PendingDiscordLinkCodeExpiresUtc = null;

        await db.SaveChangesAsync(ct);
        return (LinkResult.Success, user);
    }
}
