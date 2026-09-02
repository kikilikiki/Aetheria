using Aetheria.Database.Context;
using Aetheria.Shared.Enums;
using Aetheria.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Web.Pages.Admin;

public sealed class IndexModel(AetheriaDbContext db, DiscordTicketService discord) : PageModel
{
    public int Pending { get; private set; }
    public int Approved { get; private set; }
    public int Rejected { get; private set; }
    public int Accounts { get; private set; }
    public bool DiscordConfigured => discord.IsConfigured;
    public string? DiscordGuildId => discord.GuildId;

    public async Task OnGetAsync()
    {
        Pending = await db.BetaApplications.CountAsync(a => a.Status == BetaApplicationStatus.Pending);
        Approved = await db.BetaApplications.CountAsync(a => a.Status == BetaApplicationStatus.Approved);
        Rejected = await db.BetaApplications.CountAsync(a => a.Status == BetaApplicationStatus.Rejected);
        Accounts = await db.Users.CountAsync(u => !u.IsDeleted);
    }
}
