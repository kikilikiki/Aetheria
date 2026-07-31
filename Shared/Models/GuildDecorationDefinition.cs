namespace Aetheria.Shared.Models;

/// <summary>
/// Voir GDD/demande utilisateur — "Housing/décoration de guilde ou de royaume". **Simplification
/// assumée** : le moteur n'a pas de rendu spatial de bâtiment/pièce (voir Docs/README.md — monde
/// en grille, pas de sprite dédié) — une décoration achetée par la guilde se traduit par une
/// couleur d'accent affichée derrière le nom de la guilde (voir DrawGuildPanel côté Client), un
/// peu comme les couleurs de variante de créature (voir MonsterVariantCatalog), plutôt qu'un
/// vrai décor placé dans une pièce.
/// </summary>
public sealed record GuildDecorationDefinition(string Key, string DisplayName, string Description, long Cost, float R, float G, float B);

public static class GuildDecorationCatalog
{
    public static IReadOnlyList<GuildDecorationDefinition> All { get; } =
    [
        new("banniere_ecarlate", "Bannière Écarlate", "Une grande bannière rouge frappée du blason de la guilde.", 1000, 0.85f, 0.2f, 0.2f),
        new("trophee_dragon", "Trophée de Dragon", "Le crâne d'un dragon vaincu, monté au-dessus de l'entrée.", 2500, 0.6f, 0.85f, 0.3f),
        new("fontaine_doree", "Fontaine Dorée", "Une fontaine en or massif au centre du hall.", 4000, 0.95f, 0.8f, 0.3f),
        new("statue_fondateur", "Statue du Fondateur", "Une statue à l'effigie du chef fondateur de la guilde.", 6000, 0.7f, 0.7f, 0.75f),
        new("jardin_suspendu", "Jardin Suspendu", "Des jardins luxuriants suspendus le long des murs du hall.", 3000, 0.4f, 0.8f, 0.45f),
        new("tapis_royal", "Tapis Royal", "Un tapis pourpre déroulé depuis l'entrée jusqu'au trône.", 1500, 0.55f, 0.25f, 0.7f),
        new("vitrail_ancien", "Vitrail Ancien", "Un vitrail multicolore laissant filtrer une lumière irisée.", 3500, 0.35f, 0.6f, 0.9f),
        new("blason_legendaire", "Blason Légendaire", "Le blason de la guilde, sculpté dans un métal rare.", 8000, 0.9f, 0.65f, 0.15f),
    ];

    public static GuildDecorationDefinition? Find(string key) => All.FirstOrDefault(d => d.Key == key);
}
