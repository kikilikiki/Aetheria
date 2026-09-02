using System.Security.Claims;
using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Shared.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Web.Pages.Admin;

/// <summary>
/// Traitement des candidatures bêta. Les actions ne touchent que la base : le serveur de jeu
/// (<c>Server/Discord/BetaTicketProcessor</c>) répercute ensuite la décision dans le salon Discord
/// et attribue le rôle « Testeur » — le portail ne parle jamais à Discord.
/// </summary>
public sealed class CandidaturesModel(AetheriaDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Filter { get; set; } = "pending";

    public IReadOnlyList<BetaApplicationEntity> Applications { get; private set; } = [];
    public string? Flash { get; private set; }
    public string? DiscordGuildId { get; private set; }

    public async Task OnGetAsync()
    {
        Flash = TempData["Flash"] as string;
        DiscordGuildId = Environment.GetEnvironmentVariable("DISCORD_BETA_GUILD_ID")?.Trim()
            ?? Environment.GetEnvironmentVariable("DISCORD_GUILD_IDS")?.Split(',').FirstOrDefault()?.Trim();

        var query = db.BetaApplications.AsNoTracking().OrderByDescending(a => a.CreatedAtUtc).AsQueryable();

        query = Filter switch
        {
            "approved" => query.Where(a => a.Status == BetaApplicationStatus.Approved),
            "rejected" => query.Where(a => a.Status == BetaApplicationStatus.Rejected),
            "all" => query,
            _ => query.Where(a => a.Status == BetaApplicationStatus.Pending),
        };

        Applications = await query.ToListAsync();
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id, bool grantTester)
    {
        var (application, reviewer) = await FindAsync(id);
        if (application is null)
        {
            return RedirectToPage(new { Filter });
        }

        application.Status = BetaApplicationStatus.Approved;
        application.ReviewedByUsername = reviewer;
        application.ReviewedAtUtc = DateTime.UtcNow;

        if (grantTester)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == application.UserId);
            if (user is not null)
            {
                if (user.Rank == UserRank.Joueur)
                {
                    user.Rank = UserRank.Testeur;
                }

                await Aetheria.Database.Services.ReferralService.EnsureCodeAsync(db, user);
            }
        }

        await Aetheria.Database.Services.ReferralService.ApplyOnApprovalAsync(db, application);
        await db.SaveChangesAsync();

        TempData["Flash"] = $"Candidature de {application.Username} acceptée. Le serveur de jeu poste la confirmation dans le salon Discord.";
        return RedirectToPage(new { Filter });
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id, string? note)
    {
        var (application, reviewer) = await FindAsync(id);
        if (application is null)
        {
            return RedirectToPage(new { Filter });
        }

        application.Status = BetaApplicationStatus.Rejected;
        application.AdminNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        application.ReviewedByUsername = reviewer;
        application.ReviewedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        TempData["Flash"] = $"Candidature de {application.Username} refusée.";
        return RedirectToPage(new { Filter });
    }

    private async Task<(BetaApplicationEntity?, string)> FindAsync(Guid id)
    {
        var reviewer = User.FindFirstValue(ClaimTypes.Name) ?? "un admin";
        var application = await db.BetaApplications.FirstOrDefaultAsync(a => a.Id == id);
        return (application, reviewer);
    }
}
