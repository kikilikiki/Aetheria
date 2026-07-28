using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/users/{userId}/set-rank</c> — voir GDD, grade assignable par un administrateur.</summary>
public sealed class AdminSetRankRequest
{
    public required string SessionToken { get; init; }
    public required UserRank Rank { get; init; }
}
