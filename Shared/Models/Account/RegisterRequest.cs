namespace Aetheria.Shared.Models.Account;

/// <summary>Corps JSON de <c>POST /api/account/register</c>.</summary>
public sealed class RegisterRequest
{
    public required string Username { get; init; }
    public required string Email { get; init; }
    public required string Password { get; init; }
}
