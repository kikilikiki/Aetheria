namespace Aetheria.Shared.Enums;

/// <summary>
/// Effet spécial d'une case de la grille de combat tactique (voir GDD/demande utilisateur —
/// "pièges posés sur les cases, cases destructibles, cases de lave, cases glacées"). Voir
/// <c>Server/World/Combat/CombatEngine.cs</c> pour les effets réels, et
/// <c>Shared/Models/Combat/CombatSessionState.cs</c> pour l'exposition au Client (affichage de la
/// grille).
/// </summary>
public enum TileEffect
{
    None,
    Lave,
    Glace,
    Piege,
    Destructible,
}
