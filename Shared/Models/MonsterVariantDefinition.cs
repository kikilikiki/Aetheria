using System.Globalization;
using System.Text;
using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>
/// Statistiques/rareté d'apparition/difficulté de capture d'une <see cref="MonsterVariant"/> —
/// voir <see cref="MonsterVariantCatalog"/>. Couleur exprimée en composantes 0-1 (pas de
/// dépendance à un type de rendu depuis Shared) pour l'affichage badge côté Client.
/// </summary>
public sealed record MonsterVariantDefinition(
    MonsterVariant Variant,
    string DisplayName,
    double StatMultiplier,
    double SpawnWeight,
    double CaptureRateMultiplier,
    float ColorR,
    float ColorG,
    float ColorB);

/// <summary>
/// Catalogue des variantes de créature (voir GDD/demande utilisateur — liste des variantes,
/// "il y a déjà : Normal/Shiny/Alpha/Corrompu/Ancestral" + 12 nouvelles). Chaque variante a un
/// bonus de statistiques, un poids d'apparition en rencontre sauvage (voir
/// <c>CombatService.StartAsync</c> — plus le poids est élevé, plus elle apparaît souvent ; Normal
/// domine largement) et un multiplicateur de taux de capture (moins de 1 = plus dur à capturer,
/// compense le bonus de statistiques — voir <c>CaptureService</c>).
/// </summary>
public static class MonsterVariantCatalog
{
    public static IReadOnlyList<MonsterVariantDefinition> All { get; } =
    [
        new(MonsterVariant.Normal, "Normal", 1.00, 1000, 1.00, 0.85f, 0.85f, 0.85f),
        new(MonsterVariant.Shiny, "Brillant", 1.15, 15, 0.70, 1.00f, 0.92f, 0.30f),
        new(MonsterVariant.Alpha, "Alpha", 1.35, 8, 0.50, 0.90f, 0.30f, 0.25f),
        new(MonsterVariant.Corrompu, "Corrompu", 1.25, 10, 0.60, 0.45f, 0.15f, 0.55f),
        new(MonsterVariant.Ancestral, "Ancestral", 1.50, 5, 0.40, 0.60f, 0.50f, 0.35f),
        new(MonsterVariant.Cristallin, "Cristallin", 1.20, 12, 0.65, 0.55f, 0.85f, 0.95f),
        new(MonsterVariant.Spectral, "Spectral", 1.30, 9, 0.55, 0.65f, 0.70f, 0.95f),
        new(MonsterVariant.Dore, "Doré", 1.20, 10, 0.60, 1.00f, 0.84f, 0.20f),
        new(MonsterVariant.Maudit, "Maudit", 1.30, 8, 0.50, 0.35f, 0.05f, 0.10f),
        new(MonsterVariant.Eternel, "Éternel", 1.60, 4, 0.35, 0.75f, 0.90f, 1.00f),
        new(MonsterVariant.Divin, "Divin", 1.80, 2, 0.25, 1.00f, 0.97f, 0.85f),
        new(MonsterVariant.Infernal, "Infernal", 1.50, 5, 0.40, 0.90f, 0.25f, 0.05f),
        new(MonsterVariant.Celeste, "Céleste", 1.60, 3, 0.30, 0.60f, 0.75f, 1.00f),
        new(MonsterVariant.Mutant, "Mutant", 1.40, 6, 0.45, 0.40f, 0.80f, 0.20f),
        new(MonsterVariant.Chromatique, "Chromatique", 1.35, 7, 0.50, 0.80f, 0.40f, 0.90f),
        new(MonsterVariant.Primordial, "Primordial", 1.90, 1.5, 0.20, 0.30f, 0.55f, 0.35f),
        new(MonsterVariant.Titan, "Titan", 2.00, 1, 0.15, 0.50f, 0.50f, 0.55f),
    ];

    private static readonly Dictionary<MonsterVariant, MonsterVariantDefinition> ByVariant =
        All.ToDictionary(d => d.Variant);

    public static MonsterVariantDefinition Get(MonsterVariant variant) => ByVariant[variant];

    /// <summary>
    /// Voir demande utilisateur — commandes/panneau admin sur les variantes : résout un texte
    /// saisi à la main (nom d'enum "Shiny" ou nom d'affichage "Brillant"/"Doré"), insensible à la
    /// casse et aux accents. Retourne <c>false</c> pour une saisie vide ou inconnue.
    /// </summary>
    public static bool TryParse(string? text, out MonsterVariant variant)
    {
        variant = MonsterVariant.Normal;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = Normalize(text);
        foreach (var definition in All)
        {
            if (Normalize(definition.Variant.ToString()) == normalized || Normalize(definition.DisplayName) == normalized)
            {
                variant = definition.Variant;
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().ToUpperInvariant();
    }

    /// <summary>
    /// Tire une variante pondérée par <see cref="MonsterVariantDefinition.SpawnWeight"/> — voir
    /// CombatService.StartAsync/StartWildEncounterAsync (rencontres sauvages). Voir demande
    /// utilisateur — "augmenter les chances d'avoir des monstres modifiés" : pendant un boost, le
    /// poids de toutes les variantes non-Normal est multiplié par <paramref name="rareWeightMultiplier"/>
    /// (voir GlobalEventService.RareVariantWeightMultiplier).
    /// </summary>
    public static MonsterVariant RollWeighted(Random random, double rareWeightMultiplier = 1.0)
    {
        double Weight(MonsterVariantDefinition d) => d.Variant == MonsterVariant.Normal ? d.SpawnWeight : d.SpawnWeight * rareWeightMultiplier;

        var totalWeight = All.Sum(Weight);
        var roll = random.NextDouble() * totalWeight;
        var cumulative = 0.0;
        foreach (var definition in All)
        {
            cumulative += Weight(definition);
            if (roll < cumulative)
            {
                return definition.Variant;
            }
        }

        return MonsterVariant.Normal;
    }
}
