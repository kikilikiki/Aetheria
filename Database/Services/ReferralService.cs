using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Database.Services;

/// <summary>
/// Parrainage entre testeurs (voir demande utilisateur — « tous les testeurs ont un code avec un
/// lien »). Un lien de parrainage est <c>&lt;site&gt;/beta?ref=&lt;code&gt;</c>. Partagé par le
/// portail web et le serveur de jeu (les deux références <c>Aetheria.Database</c>).
///
/// <b>Aucune récompense n'est encore distribuée</b> : on se contente d'enregistrer le lien
/// (<see cref="UserEntity.ReferredByUserId"/>) pour pouvoir récompenser plus tard.
/// </summary>
public static class ReferralService
{
    // Sans caractères ambigus (0/O, 1/I/L).
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 6;

    /// <summary>Un compte de grade Testeur ou plus a droit à un code de parrainage.</summary>
    public static bool IsEligible(UserEntity user) =>
        user.IsAdmin || user.Rank is UserRank.Testeur or UserRank.Ami or UserRank.Moderateur or UserRank.Fondateur;

    /// <summary>
    /// Génère et enregistre un code de parrainage unique pour le compte s'il y a droit et n'en a
    /// pas déjà un. Retourne le code (existant ou nouveau), ou <c>null</c> si le compte n'y a pas droit.
    /// N'appelle PAS <c>SaveChanges</c> — l'appelant le fait.
    /// </summary>
    public static async Task<string?> EnsureCodeAsync(AetheriaDbContext db, UserEntity user, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(user.ReferralCode))
        {
            return user.ReferralCode;
        }

        if (!IsEligible(user))
        {
            return null;
        }

        string code;
        do
        {
            code = string.Concat(Enumerable.Range(0, CodeLength).Select(_ => Alphabet[Random.Shared.Next(Alphabet.Length)]));
        }
        while (await db.Users.AnyAsync(u => u.ReferralCode == code, ct));

        user.ReferralCode = code;
        return code;
    }

    /// <summary>
    /// À appeler quand une candidature bêta est acceptée : si elle porte un code de parrainage
    /// valide (celui d'un autre testeur), enregistre le lien sur le compte du filleul. Idempotent
    /// (ne réécrit pas un parrain déjà défini). N'appelle pas <c>SaveChanges</c>.
    /// </summary>
    public static async Task ApplyOnApprovalAsync(AetheriaDbContext db, BetaApplicationEntity application, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(application.ReferralCodeUsed))
        {
            return;
        }

        var applicant = await db.Users.FirstOrDefaultAsync(u => u.Id == application.UserId, ct);
        if (applicant is null || applicant.ReferredByUserId is not null)
        {
            return;
        }

        var referrer = await db.Users.FirstOrDefaultAsync(u => u.ReferralCode == application.ReferralCodeUsed, ct);
        if (referrer is null || referrer.Id == applicant.Id)
        {
            return;
        }

        applicant.ReferredByUserId = referrer.Id;
        // TODO récompenses : créditer le parrain (referrer) et le filleul (applicant) quand le
        // système de récompenses sera branché. Voir demande utilisateur (« n'en mets pas encore »).
    }
}
