using Aetheria.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aetheria.Web.Pages;

public sealed class InscriptionModel(WebAccountService accounts) : PageModel
{
    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string? Error { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await accounts.RegisterAsync(Username, Email, Password);
        if (!result.Success || result.User is null)
        {
            Error = result.Error;
            return Page();
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            WebAccountService.BuildPrincipal(result.User),
            new AuthenticationProperties { IsPersistent = true });

        return LocalRedirect("/mon-compte");
    }
}
