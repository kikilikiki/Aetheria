using System.Security.Claims;
using Aetheria.Database.Context;
using Aetheria.Database.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Web.Pages;

public sealed class CodesModel(AetheriaDbContext db) : PageModel
{
    [BindProperty] public string Code { get; set; } = string.Empty;

    public string? Message { get; private set; }
    public bool Success { get; private set; }
    public IReadOnlyList<(string Code, DateTime When)> History { get; private set; } = [];

    private async Task LoadHistoryAsync(Guid userId)
    {
        History = await db.GiftCodeRedemptions.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.RedeemedAtUtc)
            .Select(r => new ValueTuple<string, DateTime>(r.Code, r.RedeemedAtUtc))
            .ToListAsync();
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return RedirectToPage("/Connexion");
        }

        await LoadHistoryAsync(userId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return RedirectToPage("/Connexion");
        }

        var result = await GiftCodeRedeemer.RedeemAsync(db, userId, Code, "site");
        Message = result.Message;
        Success = result.Success;
        if (Success)
        {
            Code = string.Empty;
        }

        await LoadHistoryAsync(userId);
        return Page();
    }
}
