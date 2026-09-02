using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aetheria.Web.Pages;

/// <summary>
/// Point de téléchargement du jeu, réservé aux bêta-testeurs (policy « Testeur » appliquée par
/// convention dans <c>Program.cs</c>). Redirige vers l'asset GitHub Releases correspondant.
/// </summary>
public sealed class TelechargementModel : PageModel
{
    private const string ReleaseBase = "https://github.com/kikilikiki/Aetheria/releases/latest/download/";

    private static readonly Dictionary<string, string> Assets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["windows"] = ReleaseBase + "AetheriaSetup.exe",
        ["deb"] = ReleaseBase + "aetheria-amd64.deb",
        ["tar"] = ReleaseBase + "aetheria-linux-x64.tar.gz",
    };

    public IActionResult OnGet(string? os) =>
        os is not null && Assets.TryGetValue(os, out var url)
            ? Redirect(url)
            : NotFound();
}
