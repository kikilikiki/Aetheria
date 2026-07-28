namespace Aetheria.Shared.Models.Admin;

/// <summary>Requête pour poster une annonce de mise à jour dans le salon Discord du projet (voir DiscordAnnouncer).</summary>
public sealed class DiscordAnnounceRequest
{
    public required string SessionToken { get; init; }
    public required string Title { get; init; }
    public string Description { get; init; } = string.Empty;
    public List<string> Changes { get; init; } = [];
}
