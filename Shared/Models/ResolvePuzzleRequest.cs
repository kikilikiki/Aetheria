namespace Aetheria.Shared.Models;

/// <summary>
/// Corps JSON de <c>POST .../rooms/{roomIndex}/resolve-puzzle</c> (voir Docs/Idees.md — salle
/// Énigme) : choix binaire (0 ou 1) résolu côté serveur — le client n'envoie que le choix, jamais
/// le résultat, pour éviter toute triche.
/// </summary>
public sealed class ResolvePuzzleRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required int ChoiceIndex { get; init; }
}
