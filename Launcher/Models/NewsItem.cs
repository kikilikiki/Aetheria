namespace Aetheria.Launcher.Models;

/// <summary>
/// Actualité affichée dans le Launcher (voir GDD — titre, description courte, contenu complet,
/// date/heure de publication). Contenu statique pour cette première version — voir
/// <c>Docs/README.md</c> pour un futur flux géré côté serveur.
/// </summary>
public sealed class NewsItem
{
    public required string Title { get; init; }
    public required string ShortDescription { get; init; }
    public required string FullContent { get; init; }
    public required DateTime PublishedAtUtc { get; init; }

    public string PublishedLabel => $"Publié le {PublishedAtUtc:dd/MM/yyyy} à {PublishedAtUtc:HH:mm}";
}
