using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models.Admin;

/// <summary>
/// Corps JSON de <c>POST /api/admin/game/set-monster-variant</c> — voir demande utilisateur :
/// "pour modifier" un monstre déjà possédé par un joueur (variante shiny/autre, et niveau).
/// </summary>
public sealed class AdminSetMonsterVariantRequest
{
    public required string SessionToken { get; init; }
    public required string TargetCharacterName { get; init; }

    /// <summary>Surnom du monstre, ou nom de son espèce (recherche insensible casse/accents).</summary>
    public required string MonsterName { get; init; }

    public required MonsterVariant Variant { get; init; }

    /// <summary>Niveau optionnel — inchangé si absent.</summary>
    public int? Level { get; init; }
}
