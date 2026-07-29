namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/friends/request</c>, <c>/remove</c>.</summary>
public sealed class FriendActionRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required string TargetCharacterName { get; init; }
}

/// <summary>Corps JSON de <c>POST /api/friends/respond</c>.</summary>
public sealed class FriendRespondRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required string RequesterCharacterName { get; init; }
    public bool Accept { get; init; }
}
