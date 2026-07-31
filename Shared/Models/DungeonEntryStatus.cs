namespace Aetheria.Shared.Models;

/// <summary>Voir GDD/demande utilisateur — "ajoute un cooldown de 1h avant que il puisse retourne dans le dongon ou il vient d'aller".</summary>
public sealed class DungeonEntryStatus
{
    public bool Allowed { get; set; } = true;
    public DateTime? AvailableAtUtc { get; set; }
}
