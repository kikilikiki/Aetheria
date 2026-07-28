using System.Globalization;
using System.Numerics;
using System.Text;

namespace Aetheria.Engine.Rendering;

/// <summary>
/// Rendu de texte minimal via une police "à points" 3x5 dessinée avec des quads colorés
/// (<see cref="SpriteBatch.Draw"/>) — aucun fichier de police ni atlas de glyphes n'existe dans
/// le projet, donc chaque caractère est une petite grille de pixels codée en dur ci-dessous.
/// Suffisant pour dialogues/étiquettes/menus tant qu'aucune vraie police pixel art n'est
/// intégrée (voir <c>Docs/README.md</c>). Les accents ne sont pas dessinés : le texte est
/// d'abord dépouillé de ses diacritiques (é -&gt; e, ç -&gt; c, ...).
/// </summary>
public static class TextRenderer
{
    private const int GlyphColumns = 3;
    private const int GlyphRows = 5;

    private static readonly Dictionary<char, string[]> Glyphs = BuildGlyphs();

    public static float MeasureWidth(string text, float pixelSize)
    {
        var glyphAdvance = (GlyphColumns + 1) * pixelSize;
        var lineWidth = 0f;
        var maxWidth = 0f;
        foreach (var ch in text)
        {
            if (ch == '\n')
            {
                maxWidth = MathF.Max(maxWidth, lineWidth);
                lineWidth = 0f;
                continue;
            }

            lineWidth += glyphAdvance;
        }

        return MathF.Max(maxWidth, lineWidth);
    }

    public static float LineHeight(float pixelSize) => (GlyphRows + 2) * pixelSize;

    /// <summary>Dessine du texte multi-lignes, origine en haut-gauche du premier caractère.</summary>
    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, string text, Vector2 position, float pixelSize, Vector4 color)
    {
        var cursor = position;
        var glyphAdvance = (GlyphColumns + 1) * pixelSize;

        foreach (var ch in StripDiacritics(text))
        {
            if (ch == '\n')
            {
                cursor = new Vector2(position.X, cursor.Y + LineHeight(pixelSize));
                continue;
            }

            if (Glyphs.TryGetValue(char.ToUpperInvariant(ch), out var rows))
            {
                for (var row = 0; row < rows.Length; row++)
                {
                    var line = rows[row];
                    for (var col = 0; col < line.Length; col++)
                    {
                        if (line[col] == '#')
                        {
                            var pixelPos = cursor + new Vector2(col * pixelSize, row * pixelSize);
                            spriteBatch.Draw(pixel, pixelPos, new Vector2(pixelSize, pixelSize), color);
                        }
                    }
                }
            }

            cursor += new Vector2(glyphAdvance, 0f);
        }
    }

    /// <summary>Comme <see cref="Draw"/> mais centré horizontalement sur <paramref name="topCenter"/> (une seule ligne).</summary>
    public static void DrawCentered(SpriteBatch spriteBatch, Texture2D pixel, string text, Vector2 topCenter, float pixelSize, Vector4 color)
    {
        var width = MeasureWidth(text, pixelSize);
        Draw(spriteBatch, pixel, text, topCenter - new Vector2(width / 2f, 0f), pixelSize, color);
    }

    private static string StripDiacritics(string text)
    {
        var withoutLigatures = text.Replace('œ', 'o').Replace('Œ', 'O').Replace('æ', 'a').Replace('Æ', 'A');
        var normalized = withoutLigatures.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static Dictionary<char, string[]> BuildGlyphs()
    {
        var glyphs = new Dictionary<char, string[]>
        {
            [' '] = ["...", "...", "...", "...", "..."],

            ['0'] = ["###", "#.#", "#.#", "#.#", "###"],
            ['1'] = [".#.", "##.", ".#.", ".#.", "###"],
            ['2'] = ["##.", "..#", ".#.", "#..", "###"],
            ['3'] = ["##.", "..#", ".##", "..#", "##."],
            ['4'] = ["#.#", "#.#", "###", "..#", "..#"],
            ['5'] = ["###", "#..", "##.", "..#", "##."],
            ['6'] = [".##", "#..", "###", "#.#", "###"],
            ['7'] = ["###", "..#", ".#.", ".#.", ".#."],
            ['8'] = [".#.", "#.#", ".#.", "#.#", ".#."],
            ['9'] = ["###", "#.#", "###", "..#", ".#."],

            ['A'] = [".#.", "#.#", "###", "#.#", "#.#"],
            ['B'] = ["##.", "#.#", "##.", "#.#", "##."],
            ['C'] = [".##", "#..", "#..", "#..", ".##"],
            ['D'] = ["##.", "#.#", "#.#", "#.#", "##."],
            ['E'] = ["###", "#..", "##.", "#..", "###"],
            ['F'] = ["###", "#..", "##.", "#..", "#.."],
            ['G'] = [".##", "#..", "#.#", "#.#", ".##"],
            ['H'] = ["#.#", "#.#", "###", "#.#", "#.#"],
            ['I'] = ["###", ".#.", ".#.", ".#.", "###"],
            ['J'] = ["..#", "..#", "..#", "#.#", ".#."],
            ['K'] = ["#.#", "#.#", "##.", "#.#", "#.#"],
            ['L'] = ["#..", "#..", "#..", "#..", "###"],
            ['M'] = ["#.#", "###", "#.#", "#.#", "#.#"],
            ['N'] = ["#.#", "###", "#.#", "###", "#.#"],
            ['O'] = [".#.", "#.#", "#.#", "#.#", ".#."],
            ['P'] = ["##.", "#.#", "##.", "#..", "#.."],
            ['Q'] = [".#.", "#.#", "#.#", "##.", "..#"],
            ['R'] = ["##.", "#.#", "##.", "#.#", "#.#"],
            ['S'] = [".##", "#..", ".#.", "..#", "##."],
            ['T'] = ["###", ".#.", ".#.", ".#.", ".#."],
            ['U'] = ["#.#", "#.#", "#.#", "#.#", ".#."],
            ['V'] = ["#.#", "#.#", "#.#", ".#.", ".#."],
            ['W'] = ["#.#", "#.#", "#.#", "###", "#.#"],
            ['X'] = ["#.#", "#.#", ".#.", "#.#", "#.#"],
            ['Y'] = ["#.#", "#.#", ".#.", ".#.", ".#."],
            ['Z'] = ["###", "..#", ".#.", "#..", "###"],

            ['.'] = ["...", "...", "...", "...", ".#."],
            [','] = ["...", "...", "...", ".#.", "#.."],
            ['!'] = [".#.", ".#.", ".#.", "...", ".#."],
            ['?'] = ["##.", "..#", ".#.", "...", ".#."],
            [':'] = ["...", ".#.", "...", ".#.", "..."],
            ['\''] = [".#.", ".#.", "...", "...", "..."],
            ['-'] = ["...", "...", "###", "...", "..."],
            ['('] = [".#.", "#..", "#..", "#..", ".#."],
            [')'] = [".#.", "..#", "..#", "..#", ".#."],
            ['/'] = ["..#", "..#", ".#.", "#..", "#.."],
            ['%'] = ["#.#", "..#", ".#.", "#..", "#.#"],
            ['«'] = [".#.", "#..", ".#.", "#..", "..."],
            ['»'] = [".#.", "..#", ".#.", "..#", "..."],
        };

        return glyphs;
    }
}
