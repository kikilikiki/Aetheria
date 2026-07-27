namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/guilds</c>. Le personnage fondateur devient chef de guilde.</summary>
public sealed class CreateGuildRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required string Name { get; init; }
}
