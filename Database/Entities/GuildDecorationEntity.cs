namespace Aetheria.Database.Entities;

/// <summary>Décoration achetée par une guilde (table <c>GuildDecorations</c>) — voir GDD/demande utilisateur "Housing/décoration de guilde ou de royaume", GuildDecorationCatalog pour le catalogue.</summary>
public sealed class GuildDecorationEntity
{
    public Guid Id { get; set; }

    public Guid GuildId { get; set; }
    public GuildEntity? Guild { get; set; }

    public required string DecorationKey { get; set; }
    public DateTime PurchasedAtUtc { get; set; } = DateTime.UtcNow;
}
