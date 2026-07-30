namespace Aetheria.Shared.Models;

/// <summary>Une ligne du coffre partagé de guilde — voir GDD/demande utilisateur "Coffre partagé".</summary>
public sealed record GuildChestItemSummary(int ItemId, string ItemName, int Quantity);
