using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models.Admin;

/// <summary>Vue joueur pour l'AdminPanel (voir <c>Docs/GameDesign.md</c> — section AdminPanel).</summary>
public sealed class AdminUserSummary
{
    public required Guid Id { get; init; }
    public required string Username { get; init; }
    public required string Email { get; init; }
    public required bool IsBanned { get; init; }
    public string? BanReason { get; init; }
    public required bool IsAdmin { get; init; }
    public required bool IsDeleted { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required int CharacterCount { get; init; }
    public required UserRank Rank { get; init; }
}
