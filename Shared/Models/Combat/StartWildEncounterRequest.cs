namespace Aetheria.Shared.Models.Combat;

/// <summary>
/// Corps JSON de <c>POST /api/combat/start-wild</c> — rencontre aléatoire hors donjon (voir GDD),
/// pas d'espèce choisie par le client comme <see cref="StartCombatRequest"/> : le serveur la
/// tire lui-même, scalée sur le niveau du chef de groupe (voir <c>PartyService.ResolveScalingReferenceAsync</c>).
/// </summary>
public sealed class StartWildEncounterRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required IReadOnlyList<Guid> MonsterIds { get; init; }
}
