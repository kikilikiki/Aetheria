namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/game/give-battlepass-level</c> — voir GDD/demande utilisateur "ajoute une commande et un champ admin pour donner des palier a un joueur" (paliers du Passe de Niveau).</summary>
public sealed class AdminGiveBattlePassLevelRequest
{
    public required string SessionToken { get; init; }
    public required string TargetCharacterName { get; init; }
    public required int Levels { get; init; }
}
