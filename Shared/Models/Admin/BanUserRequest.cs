namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/users/{id}/ban</c>.</summary>
public sealed class BanUserRequest
{
    public required string Reason { get; init; }
}
