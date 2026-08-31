namespace Aetheria.Shared.Models.Admin;

/// <summary>
/// Corps JSON de <c>POST /api/admin/game/spawn-dungeon</c> — voir demande utilisateur : "ajoute
/// une commande admin et un bouton dans le panel pour faire apparaître un donjon spécifique".
/// Ajoute un 3ᵉ portail temporaire (visible par tous) jusqu'à la rotation horaire suivante.
/// </summary>
public sealed class AdminSpawnDungeonRequest
{
    public required string SessionToken { get; init; }
    public required string DungeonName { get; init; }
}
