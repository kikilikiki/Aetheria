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
    public bool ReferralPending { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return RedirectToPage("/Connexion");
        }

        var account = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (account is null)
        {
            return RedirectToPage("/Connexion");
        }

        // Le code de parrainage est attribué (et journalisé sur Discord) par le serveur de jeu,
        // pas ici — voir Server/Discord/BetaTicketProcessor.EnsureReferralCodesAsync. Il apparaît
        // en quelques secondes après l'obtention du grade Testeur.
        ReferralPending = Aetheria.Database.Services.ReferralService.IsEligible(account) && string.IsNullOrEmpty(account.ReferralCode);

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
