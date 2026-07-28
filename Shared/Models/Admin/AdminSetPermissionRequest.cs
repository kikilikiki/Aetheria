namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/users/{id}/set-admin</c>.</summary>
public sealed class AdminSetPermissionRequest
{
    public required string SessionToken { get; init; }
    public required bool IsAdmin { get; init; }
}
