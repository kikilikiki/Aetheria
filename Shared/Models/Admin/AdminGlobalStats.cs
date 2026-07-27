namespace Aetheria.Shared.Models.Admin;

/// <summary>Statistiques globales du serveur pour l'AdminPanel.</summary>
public sealed class AdminGlobalStats
{
    public required int TotalUsers { get; init; }
    public required int BannedUsers { get; init; }
    public required int TotalCharacters { get; init; }
    public required int TotalMonstersCaptured { get; init; }
    public required int TotalGuilds { get; init; }
    public required int ActiveSeasonNumber { get; init; }
}
