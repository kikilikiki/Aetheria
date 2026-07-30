namespace Aetheria.Database.Entities;

/// <summary>
/// Coffre partagé de guilde (table <c>GuildChestItems</c>) — voir GDD/demande utilisateur "Coffre
/// partagé". N'importe quel membre peut déposer/retirer (voir GuildService.DepositItemAsync/
/// WithdrawItemAsync) — pas de verrou par grade dans cette première version.
/// </summary>
public sealed class GuildChestItemEntity
{
    public Guid Id { get; set; }

    public Guid GuildId { get; set; }
    public GuildEntity? Guild { get; set; }

    public int ItemId { get; set; }
    public int Quantity { get; set; }
}
