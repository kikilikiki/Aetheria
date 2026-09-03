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

    // Voir demande utilisateur — le Fondateur choisit ce que donne le code : gemmes, or, créature, et/ou texte libre.
    [BindProperty] public long NewGems { get; set; }
    [BindProperty] public long NewGold { get; set; }
    [BindProperty] public int? NewMonsterSpeciesId { get; set; }
    [BindProperty] public int NewMonsterLevel { get; set; } = 1;
    [BindProperty] public Aetheria.Shared.Enums.MonsterVariant NewMonsterVariant { get; set; } = Aetheria.Shared.Enums.MonsterVariant.Normal;

    public IReadOnlyList<(int Id, string Name)> Species { get; private set; } = [];
    public IReadOnlyList<GiftCodeEntity> Codes { get; private set; } = [];
    public string? Flash { get; private set; }

    private async Task LoadAsync()
    {
        Codes = await db.GiftCodes.AsNoTracking().OrderByDescending(c => c.CreatedAtUtc).ToListAsync();
        Species = await db.MonsterSpecies.AsNoTracking().OrderBy(s => s.Id)
            .Select(s => new ValueTuple<int, string>(s.Id, s.Name)).ToListAsync();
    }

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

        if (NewMonsterSpeciesId is { } sid && !await db.MonsterSpecies.AnyAsync(s => s.Id == sid))
        {
            TempData["Flash"] = "Espèce de créature inconnue.";
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
            RewardGems = Math.Max(0, NewGems),
            RewardGold = Math.Max(0, NewGold),
            RewardMonsterSpeciesId = NewMonsterSpeciesId is > 0 ? NewMonsterSpeciesId : null,
            RewardMonsterLevel = Math.Clamp(NewMonsterLevel, 1, 150),
            RewardMonsterVariant = NewMonsterVariant,
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
