using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models.WorldBoss;

/// <summary>Corps JSON de <c>POST /api/admin/game/spawn-world-boss</c> — voir GDD/demande utilisateur "boss geant mondial", réservé aux comptes admin/fondateur.</summary>
public sealed class SpawnWorldBossRequest
{
    public required string SessionToken { get; init; }

    /// <summary>Voir GDD/demande utilisateur — "retire le champ espece et royaume pour le boss monde" : l'espèce est désormais tirée au sort côté serveur (voir WorldBossService.SpawnAsync) plutôt que choisie par l'admin, et le boss ne cible plus un royaume en particulier.</summary>
    public required int MaxHealth { get; init; }
}
