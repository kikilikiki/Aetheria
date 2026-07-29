using System.Globalization;
using System.Text;

namespace Aetheria.Server.Persistence;

/// <summary>
/// Voir GDD/demande utilisateur — "je n'arrive pas à me give de monstre" : la recherche de
/// personnage/espèce par nom exigeait une correspondance exacte (casse ET accents), ce qui
/// échouait silencieusement dès qu'un nom comme "Zéphyrin" ou "Pénombrelle" était tapé sans son
/// accent ou avec une casse différente — piège classique d'une saisie manuelle en jeu/panel admin.
/// Utilisé pour toute recherche par nom déclenchée par une saisie utilisateur (pas les clés
/// internes/techniques, qui doivent rester strictes).
/// </summary>
public static class TextMatching
{
    /// <summary>Compare deux textes en ignorant la casse et les accents/diacritiques.</summary>
    public static bool NamesMatch(string? a, string? b)
    {
        if (a is null || b is null)
        {
            return false;
        }

        return Normalize(a) == Normalize(b);
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
}
