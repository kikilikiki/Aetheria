namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/monsters/{id}/set-active-team</c> — voir GDD/demande utilisateur, "on doit équiper les monstres au lieu de juste les mettre avec soi via la pension".</summary>
public sealed class SetMonsterActiveTeamRequest
{
    public required string SessionToken { get; init; }
    public required Guid MonsterId { get; init; }

    /// <summary>Vrai pour équiper (emplacement libre assigné automatiquement), faux pour déséquiper.</summary>
    public required bool Equip { get; init; }
}
