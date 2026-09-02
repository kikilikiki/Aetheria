using System.Security.Claims;
using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Web.Pages;

public sealed class CompteModel(AetheriaDbContext db) : PageModel
{
    public UserEntity Account { get; private set; } = null!;
    public BetaApplicationEntity? Application { get; private set; }
    public int ReferralCount { get; private set; }
    public string ReferralLink { get; private set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return RedirectToPage("/Connexion");
        }

        var account = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (account is null)
        {
            return RedirectToPage("/Connexion");
        }

        // Génère le code de parrainage à la volée si le compte y a droit et n'en a pas encore
        // (ex. promu testeur via le jeu, sans passer par une acceptation de candidature).
        if (Aetheria.Database.Services.ReferralService.IsEligible(account) && string.IsNullOrEmpty(account.ReferralCode))
        {
            await Aetheria.Database.Services.ReferralService.EnsureCodeAsync(db, account);
            await db.SaveChangesAsync();
        }

        Account = account;
        Application = await db.BetaApplications.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrEmpty(account.ReferralCode))
        {
            ReferralCount = await db.Users.CountAsync(u => u.ReferredByUserId == userId);
            ReferralLink = $"{Aetheria.Shared.GameInfo.WebsiteUrl}/beta?ref={account.ReferralCode}";
        }

        return Page();
    }
}
