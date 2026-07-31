namespace Aetheria.Shared.Models.GuildRaid;

/// <summary>Corps JSON de <c>POST /api/guildraid/spawn</c> — voir GDD/demande utilisateur "Raids de guilde (boss coopératif nécessitant plusieurs joueurs)". Invocable par n'importe quel membre (pas seulement le chef), coûte de l'or à la banque de guilde.</summary>
public sealed class GuildRaidSpawnRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
}
