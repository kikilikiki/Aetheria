namespace Aetheria.Shared.Models;

/// <summary>
/// Voir Docs/Idees.md — "PvP sauvage" : grade militaire affiché selon la réputation gagnée en
/// combattant dans une zone à risque (voir <c>Server/World/Combat/WildPvpQueueService.cs</c>),
/// même principe de paliers que <c>TitleCatalog</c> pour les titres PvP classés.
/// </summary>
public static class MilitaryRankCatalog
{
    private static readonly (int Threshold, string Rank)[] Tiers =
    [
        (0, "Recrue"),
        (3, "Soldat"),
        (8, "Vétéran de guerre"),
        (15, "Capitaine"),
        (25, "Commandant"),
        (40, "Général"),
    ];

    public static string RankFor(int reputation)
    {
        var rank = Tiers[0].Rank;
        foreach (var (threshold, name) in Tiers)
        {
            if (reputation >= threshold)
            {
                rank = name;
            }
        }

        return rank;
    }
}
