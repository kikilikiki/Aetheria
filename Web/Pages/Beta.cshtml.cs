using System.Security.Claims;
using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Shared.Enums;
using Aetheria.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Web.Pages;

public sealed class BetaModel(AetheriaDbContext db, DiscordTicketService discord, ILogger<BetaModel> logger) : PageModel
{
    [BindProperty] public string DiscordHandle { get; set; } = string.Empty;
    [BindProperty] public string ContactEmail { get; set; } = string.Empty;
    [BindProperty] public string InGamePseudo { get; set; } = string.Empty;
    [BindProperty] public string Platform { get; set; } = "Windows";
    [BindProperty] public string HardwareSpecs { get; set; } = string.Empty;
    [BindProperty] public string? Notes { get; set; }

    public UserEntity Account { get; private set; } = null!;
    public BetaApplicationEntity? Existing { get; private set; }
    public bool DiscordLinked => !string.IsNullOrEmpty(Account.DiscordUserId);
    public string? Error { get; private set; }
    public bool Submitted { get; private set; }
    public bool TicketCreated { get; private set; }

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

        var handle = DiscordLinked ? Account.DiscordUserId! : DiscordHandle.Trim();

        if (!DiscordLinked && handle.Length < 2)
        {
            Error = "Indique ton pseudo Discord.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(InGamePseudo) || string.IsNullOrWhiteSpace(HardwareSpecs))
        {
            Error = "Merci de remplir le pseudo en jeu et la configuration de ton PC.";
            return Page();
        }

        // Vérification Discord AVANT toute écriture (voir demande utilisateur).
        var resolution = await discord.ResolveMemberAsync(Account, DiscordHandle);
        if (!resolution.Found)
        {
            Error = resolution.Error;
            return Page();
        }

        var application = new BetaApplicationEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Username = Account.Username,
            DiscordHandle = DiscordLinked ? (resolution.DisplayName ?? "compte lié") : DiscordHandle.Trim(),
            ContactEmail = ContactEmail.Trim(),
            InGamePseudo = InGamePseudo.Trim(),
            Platform = Platform == "Linux" ? "Linux" : "Windows",
            HardwareSpecs = HardwareSpecs.Trim(),
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            ResolvedDiscordUserId = resolution.DiscordUserId,
            Status = BetaApplicationStatus.Pending,
        };

        try
        {
            var channelId = await discord.CreateTicketAsync(application, resolution.DiscordUserId!);
            application.DiscordTicketChannelId = channelId;
            TicketCreated = channelId is not null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Création du ticket Discord en échec pour la candidature de {User}.", Account.Username);
        }

        db.BetaApplications.Add(application);
        await db.SaveChangesAsync();

        Submitted = true;
        Existing = application;
        return Page();
    }
}
