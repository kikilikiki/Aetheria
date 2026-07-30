namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/guilds</c>. Le personnage fondateur devient chef de guilde.</summary>
public sealed class CreateGuildRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required string Name { get; init; }

    /// <summary>Voir GDD/demande utilisateur — "rendre les guildes publiques/privees" : si faux, le serveur génère automatiquement un code à 5 chiffres (voir GuildSummary.JoinCode).</summary>
    public bool IsPublic { get; init; } = true;
}
