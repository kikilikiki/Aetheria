namespace Aetheria.Shared.Models.Account;

/// <summary>
/// Corps JSON de <c>POST /api/account/login</c>. <see cref="UsernameOrEmail"/> accepte
/// indifféremment le pseudo ou l'email (voir <c>Docs/GameDesign.md</c> — Système de compte).
/// </summary>
public sealed class LoginRequest
{
    public required string UsernameOrEmail { get; init; }
    public required string Password { get; init; }
}
