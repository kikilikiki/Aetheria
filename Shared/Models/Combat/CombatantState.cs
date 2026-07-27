namespace Aetheria.Shared.Models.Combat;

/// <summary>
/// L'état public d'un combattant (personnage, créature ou monstre sauvage) tel que renvoyé
/// au client — pas de données serveur internes (IA, graine aléatoire, ...).
/// </summary>
public sealed record CombatantState(
    Guid Id,
    string Name,
    int Team,
    int PositionX,
    int PositionY,
    int CurrentHealth,
    int MaxHealth,
    bool IsAlive);
