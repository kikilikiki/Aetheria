using System.Security.Claims;
using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Shared.Enums;
using Aetheria.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Web.Pages.Admin;

public sealed class CandidaturesModel(AetheriaDbContext db, DiscordTicketService discord) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Filter { get; set; } = "pending";

    public IReadOnlyList<BetaApplicationEntity> Applications { get; private set; } = [];
    public DiscordTicketService Discord => discord;
    public string? Flash { get; private set; }

    public async Task OnGetAsync()
    {
        Flash = TempData["Flash"] as string;
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
        await db.SaveChangesAsync();

        if (application.DiscordTicketChannelId is { Length: > 0 } channelId)
        {
            await discord.PostToTicketAsync(channelId, $"✅ Candidature **acceptée** par {reviewer}. Bienvenue en bêta !");
        }

        if (grantTester)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == application.UserId);
            if (user is not null && user.Rank == UserRank.Joueur)
            {
                user.Rank = UserRank.Testeur;
                await db.SaveChangesAsync();
            }

            if (application.ResolvedDiscordUserId is { Length: > 0 } discordId)
            {
                await discord.GrantTesterRoleAsync(discordId);
            }
        }

        TempData["Flash"] = $"Candidature de {application.Username} acceptée.";
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

        if (application.DiscordTicketChannelId is { Length: > 0 } channelId)
        {
            var reason = string.IsNullOrWhiteSpace(note) ? "" : $" Raison : {note.Trim()}";
            await discord.PostToTicketAsync(channelId, $"❌ Candidature **refusée** par {reviewer}.{reason}");
        }

        TempData["Flash"] = $"Candidature de {application.Username} refusée.";
        return RedirectToPage(new { Filter });
    }

    public async Task<IActionResult> OnPostArchiveAsync(Guid id)
    {
        var (application, _) = await FindAsync(id);
        if (application?.DiscordTicketChannelId is { Length: > 0 } channelId)
        {
            var ok = await discord.ArchiveTicketAsync(channelId);
            if (ok)
            {
                application.DiscordTicketChannelId = null;
                await db.SaveChangesAsync();
            }

            TempData["Flash"] = ok ? "Salon Discord archivé." : "Impossible d'archiver le salon.";
        }

        return RedirectToPage(new { Filter });
    }

    private async Task<(BetaApplicationEntity?, string)> FindAsync(Guid id)
    {
        var reviewer = User.FindFirstValue(ClaimTypes.Name) ?? "un admin";
        var application = await db.BetaApplications.FirstOrDefaultAsync(a => a.Id == id);
        return (application, reviewer);
    }
}
