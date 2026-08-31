using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/game/give-monster</c> — voir GDD/demande utilisateur "les admin et le fonda peuvent aussi donner 1 monstre à un joueur".</summary>
public sealed class AdminGiveMonsterRequest
{
    public required string SessionToken { get; init; }
    public required string TargetCharacterName { get; init; }
    public required int SpeciesId { get; init; }

    /// <summary>Voir demande utilisateur — "ajoute la possibilité d'ajouter un truc si un monstre est shiny ou autre" : variante optionnelle (Normal si absente).</summary>
    public MonsterVariant? Variant { get; init; }

    /// <summary>Niveau optionnel du monstre donné (1 si absent).</summary>
    public int? Level { get; init; }
}
