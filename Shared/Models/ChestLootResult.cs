namespace Aetheria.Shared.Models;

/// <summary>Résultat de l'ouverture d'un coffre de donjon (voir GDD/demande utilisateur — "pouvoir obtenir d'autre chose que de l'or dans les donjons"). <see cref="ItemName"/> est <c>null</c> si le coffre n'a donné que de l'or.</summary>
public sealed class ChestLootResult
{
    public required int Gold { get; init; }
    public string? ItemName { get; init; }
}
