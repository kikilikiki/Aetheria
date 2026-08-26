namespace Aetheria.Shared.Models;

/// <summary>Voir Docs/Idees.md — résultat du déclenchement d'un piège en salle de donjon (perte d'or, symétrique du gain d'un coffre).</summary>
public sealed class TrapResult
{
    public required int GoldLost { get; init; }
}
