using System.Security.Claims;
using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Shared.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Web.Pages;

/// <summary>
/// Formulaire de candidature bêta. Le portail ne contacte JAMAIS Discord (l'IP partagée de Render
/// est rate-limitée) : il enregistre la candidature en base, et le serveur de jeu
/// (<c>Server/Discord/BetaTicketProcessor</c>) vérifie la présence Discord et crée le salon
/// « ticket » quelques secondes plus tard.
/// </summary>
public sealed class BetaModel(AetheriaDbContext db) : PageModel
{
    [BindProperty] public string DiscordHandle { get; set; } = string.Empty;
    [BindProperty] public string ContactEmail { get; set; } = string.Empty;
    [BindProperty] public string InGamePseudo { get; set; } = string.Empty;
    [BindProperty] public string Platform { get; set; } = "Windows";
    [BindProperty] public string HardwareSpecs { get; set; } = string.Empty;
    [BindProperty] public string Motivation { get; set; } = string.Empty;
    [BindProperty] public string Discovery { get; set; } = string.Empty;
    [BindProperty] public string? Notes { get; set; }

    [BindProperty(SupportsGet = true, Name = "ref")]
    public string? ReferralCode { get; set; }

    public UserEntity Account { get; private set; } = null!;
    public BetaApplicationEntity? Existing { get; private set; }
    public bool DiscordLinked => !string.IsNullOrEmpty(Account.DiscordUserId);
    public string? Error { get; private set; }
    public bool Submitted { get; private set; }

    private async Task<bool> LoadAsync(Guid userId)
    {
        var account = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (account is null)
        {
            return false;
        }

        Account = account;
        Existing = await db.BetaApplications
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync();
        return true;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || !await LoadAsync(userId))
        {
            return RedirectToPage("/Connexion");
        }

        ContactEmail = Account.Email;
        InGamePseudo = Account.Username;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || !await LoadAsync(userId))
        {
            return RedirectToPage("/Connexion");
        }

        // Une candidature en attente ou déjà acceptée bloque une nouvelle soumission.
        if (Existing is { Status: BetaApplicationStatus.Pending or BetaApplicationStatus.Approved })
        {
            return Page();
        }

        if (!DiscordLinked && DiscordHandle.Trim().TrimStart('@').Length < 2)
        {
            Error = "Indique ton pseudo Discord.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(InGamePseudo) || string.IsNullOrWhiteSpace(HardwareSpecs)
            || string.IsNullOrWhiteSpace(Motivation) || string.IsNullOrWhiteSpace(Discovery))
        {
            Error = "Merci de répondre à toutes les questions obligatoires.";
            return Page();
        }

        var application = new BetaApplicationEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Username = Account.Username,
            DiscordHandle = DiscordLinked ? "(compte Discord lié)" : DiscordHandle.Trim().TrimStart('@'),
            ContactEmail = ContactEmail.Trim(),
            InGamePseudo = InGamePseudo.Trim(),
            Platform = Platform == "Linux" ? "Linux" : "Windows",
            HardwareSpecs = HardwareSpecs.Trim(),
            Motivation = Motivation.Trim(),
            Discovery = Discovery.Trim(),
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            ReferralCodeUsed = NormalizeReferral(ReferralCode),
            ResolvedDiscordUserId = DiscordLinked ? Account.DiscordUserId : null,
            Status = BetaApplicationStatus.Pending,
            // ProcessedAtUtc = null → le serveur de jeu vérifie Discord et crée le salon.
        };

        db.BetaApplications.Add(application);
        await db.SaveChangesAsync();

        Submitted = true;
        Existing = application;
        return Page();
    }

    /// <summary>Code de parrainage : majuscules, alphanumérique, borné — <c>null</c> si vide/invalide.</summary>
    internal static string? NormalizeReferral(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var cleaned = new string(raw.Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        return cleaned.Length is >= 4 and <= 16 ? cleaned : null;
    }
}
