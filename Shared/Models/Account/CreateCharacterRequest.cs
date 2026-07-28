using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models.Account;

/// <summary>Corps JSON de <c>POST /api/characters</c> — création du premier (ou nième) personnage du compte.</summary>
public sealed class CreateCharacterRequest
{
    public required string SessionToken { get; init; }
    public required string Name { get; init; }
    public required CharacterClass Class { get; init; }
    public required KingdomType Kingdom { get; init; }

    // Apparence choisie dans la scène de création en jeu (voir GDD) — indices dans de petites
    // palettes fixes définies côté Client (Client/World/CharacterAppearancePalette.cs).
    public int SkinColorIndex { get; init; }
    public int HairStyleIndex { get; init; }
    public int HairColorIndex { get; init; }
    public int ClothesColorIndex { get; init; }
    public int AccessoryIndex { get; init; }
}
