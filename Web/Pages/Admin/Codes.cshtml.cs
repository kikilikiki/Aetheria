using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Database.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Web.Pages.Admin;

/// <summary>Création / gestion des codes cadeaux (voir demande utilisateur — « n'en mets pas encore » : la liste part vide).</summary>
public sealed class CodesModel(AetheriaDbContext db) : PageModel
{
    [BindProperty] public string NewCode { get; set; } = string.Empty;
    [BindProperty] public string NewDescription { get; set; } = string.Empty;
    [BindProperty] public int? NewMaxRedemptions { get; set; }
    [BindProperty] public DateTime? NewExpiresAtUtc { get; set; }

    public IReadOnlyList<GiftCodeEntity> Codes { get; private set; } = [];
    public string? Flash { get; private set; }

    private async Task LoadAsync() =>
        Codes = await db.GiftCodes.AsNoTracking().OrderByDescending(c => c.CreatedAtUtc).ToListAsync();

    public async Task OnGetAsync()
    {
        Flash = TempData["Flash"] as string;
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        var code = GiftCodeRedeemer.Normalize(NewCode);
        if (code.Length < 3)
        {
            TempData["Flash"] = "Code trop court (3 caractères minimum).";
            return RedirectToPage();
        }

        if (await db.GiftCodes.AnyAsync(c => c.Code == code))
        {
            TempData["Flash"] = "Ce code existe déjà.";
            return RedirectToPage();
        }

        db.GiftCodes.Add(new GiftCodeEntity
        {
            Id = Guid.NewGuid(),
            Code = code,
            Description = NewDescription.Trim(),
            MaxRedemptions = NewMaxRedemptions is > 0 ? NewMaxRedemptions : null,
            ExpiresAtUtc = NewExpiresAtUtc,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        TempData["Flash"] = $"Code {code} créé.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(Guid id)
    {
        var code = await db.GiftCodes.FirstOrDefaultAsync(c => c.Id == id);
        if (code is not null)
        {
            code.IsActive = !code.IsActive;
            await db.SaveChangesAsync();
        }

        return RedirectToPage();
    }
}
