namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/users/{id}/modify</c> — champs omis (null) laissés inchangés.</summary>
public sealed class AdminModifyUserRequest
{
    public required string SessionToken { get; init; }
    public string? NewUsername { get; init; }
    public string? NewEmail { get; init; }
}
