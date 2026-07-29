using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>
/// Corps JSON de <c>POST /api/monsters/capture</c>. <see cref="TargetHealthPercent"/> simule
/// l'issue du combat préalable (0 = vaincu, 100 = pleine vie) : le moteur de combat tactique
/// complet n'existe pas encore (voir <c>Docs/README.md</c>), ce endpoint pose la mécanique de
/// capture elle-même en attendant.
/// </summary>
public sealed class CaptureAttemptRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required int SpeciesId { get; init; }
    public required int TargetHealthPercent { get; init; }
    public required int CaptureItemId { get; init; }

    /// <summary>Voir GDD/demande utilisateur — variantes de créature (voir MonsterVariantCatalog) : la variante réellement rencontrée (voir CombatService.ResolveCaptureAsync), Normal par défaut pour compatibilité avec un éventuel appel direct de cet endpoint sans passer par le combat.</summary>
    public MonsterVariant Variant { get; init; } = MonsterVariant.Normal;
}
