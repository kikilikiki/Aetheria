using System.Security.Claims;
using Aetheria.Database.Context;
using Aetheria.Shared.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Web.Services;

/// <summary>
/// Recharge le grade / les droits du compte depuis la base à chaque requête authentifiée, plutôt
/// que de se fier aux claims figés dans le cookie de connexion. Ainsi, dès qu'un admin accepte une
/// candidature (grade Testeur) ou bannit un compte, l'effet est immédiat sans que l'utilisateur ait
/// à se reconnecter — en particulier pour l'accès au téléchargement (claim <c>can_download</c>).
/// </summary>
public sealed class RankClaimsTransformation(AetheriaDbContext db) : IClaimsTransformation
{
    private const string FreshMarker = "rank_fresh";

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return principal;
        }

        // IClaimsTransformation peut être invoqué plusieurs fois par requête — ne faire le travail
        // (et l'aller-retour base) qu'une seule fois.
        if (principal.HasClaim(FreshMarker, "1"))
        {
            return principal;
        }

        foreach (var type in new[] { ClaimTypes.Role, "is_admin", "is_staff", "can_download", FreshMarker })
        {
            foreach (var claim in identity.FindAll(type).ToList())
            {
                identity.RemoveClaim(claim);
            }
        }

        identity.AddClaim(new Claim(FreshMarker, "1"));

        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return principal;
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null || user.IsDeleted || user.IsBanned)
        {
            return principal; // compte invalide : aucun droit accordé
        }

        var isStaff = user.IsAdmin || user.Rank == UserRank.Fondateur;

        identity.AddClaim(new Claim(ClaimTypes.Role, user.Rank.ToString()));
        identity.AddClaim(new Claim("is_admin", user.IsAdmin ? "true" : "false"));
        identity.AddClaim(new Claim("is_staff", isStaff ? "true" : "false"));
        identity.AddClaim(new Claim("can_download", WebAccountService.CanDownload(user) ? "true" : "false"));

        return principal;
    }
}
