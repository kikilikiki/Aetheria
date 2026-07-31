namespace Aetheria.Shared.Models;

/// <summary>Voir GDD/demande utilisateur — "a la fin des 10 etage termine le dongon [...] donne lui des recompense".</summary>
public sealed class DungeonCompletionResult
{
    public int Gold { get; set; }
    public string? ItemName { get; set; }
    public DateTime CooldownUntilUtc { get; set; }
}
