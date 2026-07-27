using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>Réponse JSON commune à la récolte et à l'artisanat.</summary>
public sealed class ProfessionActionResponse
{
    public required ProfessionType Profession { get; init; }
    public required int Level { get; init; }
    public required long Experience { get; init; }
    public required bool LeveledUp { get; init; }
    public required string Message { get; init; }
}
