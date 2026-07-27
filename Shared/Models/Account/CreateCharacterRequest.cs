using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models.Account;

/// <summary>Corps JSON de <c>POST /api/characters</c> — création du premier (ou nième) personnage du compte.</summary>
public sealed class CreateCharacterRequest
{
    public required string SessionToken { get; init; }
    public required string Name { get; init; }
    public required CharacterClass Class { get; init; }
    public required KingdomType Kingdom { get; init; }
}
