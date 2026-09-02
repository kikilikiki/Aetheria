using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Database.Services;

/// <summary>
/// Rédemption d'un <see cref="GiftCodeEntity"/> (voir demande utilisateur — codes cadeaux à saisir
/// sur le site et dans le Launcher). Partagé par le portail web et le serveur de jeu.
///
/// <b>Aucune récompense n'est encore distribuée</b> : la rédemption est enregistrée
/// (<see cref="GiftCodeRedemptionEntity"/>) et le message renvoyé décrit ce qui sera accordé plus
/// tard. Aucun code n'est créé par défaut (« n'en mets pas encore »).
/// </summary>
public static class GiftCodeRedeemer
{
    public sealed record Result(bool Success, string Message);

    /// <summary>Normalise un code saisi : majuscules, alphanumérique.</summary>
    public static string Normalize(string raw) =>
        new(raw.Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    public static async Task<Result> RedeemAsync(AetheriaDbContext db, Guid userId, string rawCode, string source, CancellationToken ct = default)
    {
        var code = Normalize(rawCode);
        if (code.Length == 0)
        {
            return new Result(false, "Saisis un code.");
        }

        var giftCode = await db.GiftCodes.FirstOrDefaultAsync(c => c.Code == code, ct);
        if (giftCode is null || !giftCode.IsActive)
        {
            return new Result(false, "Code invalide.");
        }

        if (giftCode.ExpiresAtUtc is { } expiry && expiry < DateTime.UtcNow)
        {
            return new Result(false, "Ce code a expiré.");
        }

        if (giftCode.MaxRedemptions is { } max && giftCode.RedemptionCount >= max)
        {
            return new Result(false, "Ce code a atteint sa limite d'utilisations.");
        }

        var already = await db.GiftCodeRedemptions.AnyAsync(r => r.GiftCodeId == giftCode.Id && r.UserId == userId, ct);
        if (already)
        {
            return new Result(false, "Tu as déjà utilisé ce code.");
        }

        db.GiftCodeRedemptions.Add(new GiftCodeRedemptionEntity
        {
            Id = Guid.NewGuid(),
            GiftCodeId = giftCode.Id,
            Code = giftCode.Code,
            UserId = userId,
            Source = source,
        });
        giftCode.RedemptionCount++;

        // TODO récompenses : interpréter giftCode.RewardPayload et créditer le compte quand le
        // système de récompenses sera branché.
        await db.SaveChangesAsync(ct);

        var reward = string.IsNullOrWhiteSpace(giftCode.Description) ? "Ta récompense a été enregistrée." : giftCode.Description;
        return new Result(true, $"Code accepté ! {reward}");
    }
}
