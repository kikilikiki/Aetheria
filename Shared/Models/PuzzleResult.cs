namespace Aetheria.Shared.Models;

/// <summary>Voir Docs/Idees.md — résultat d'une salle Énigme : <see cref="GoldDelta"/> est positif si le choix était le bon, négatif sinon.</summary>
public sealed class PuzzleResult
{
    public required bool WasCorrect { get; init; }
    public required int GoldDelta { get; init; }
}
