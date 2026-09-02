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

        Account = account;
        Application = await db.BetaApplications.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync();

        return Page();
    }
}
