using Aetheria.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aetheria.Web.Pages;

public sealed class ConnexionModel(WebAccountService accounts) : PageModel
{
    [BindProperty]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? Error { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(UsernameOrEmail) || string.IsNullOrWhiteSpace(Password))
        {
            Error = "Renseigne ton identifiant et ton mot de passe.";
            return Page();
        }

        var result = await accounts.VerifyAsync(UsernameOrEmail, Password);
        if (!result.Success || result.User is null)
        {
            Error = result.Error;
            return Page();
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            WebAccountService.BuildPrincipal(result.User),
            new AuthenticationProperties { IsPersistent = true });

        return LocalRedirect(string.IsNullOrEmpty(ReturnUrl) ? "/mon-compte" : ReturnUrl);
    }
}
