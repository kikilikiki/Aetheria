using System.Numerics;

namespace Aetheria.Client.World;

/// <summary>
/// Petites palettes fixes pour la personnalisation d'apparence (voir GDD — création de
/// personnage en jeu). Le moteur ne dessine que des quads colorés (pas de sprites/textures de
/// personnage), donc l'apparence est un jeu d'indices dans ces tableaux plutôt que des couleurs
/// libres ou de vraies pièces d'équipement.
/// </summary>
public static class CharacterAppearancePalette
{
    public static readonly (string Name, Vector4 Color)[] SkinColors =
    [
        ("Claire", new Vector4(0.92f, 0.80f, 0.68f, 1f)),
        ("Hâlée", new Vector4(0.82f, 0.62f, 0.45f, 1f)),
        ("Foncée", new Vector4(0.55f, 0.38f, 0.26f, 1f)),
        ("Pâle", new Vector4(0.95f, 0.90f, 0.85f, 1f)),
        ("Verte", new Vector4(0.55f, 0.72f, 0.45f, 1f)),
    ];

    public static readonly (string Name, Vector4 Color)[] HairColors =
    [
        ("Noir", new Vector4(0.12f, 0.10f, 0.10f, 1f)),
        ("Brun", new Vector4(0.35f, 0.22f, 0.14f, 1f)),
        ("Blond", new Vector4(0.85f, 0.72f, 0.35f, 1f)),
        ("Roux", new Vector4(0.72f, 0.32f, 0.15f, 1f)),
        ("Blanc", new Vector4(0.92f, 0.92f, 0.92f, 1f)),
        ("Bleu", new Vector4(0.30f, 0.45f, 0.80f, 1f)),
    ];

    public static readonly (string Name, Vector4 Color)[] ClothesColors =
    [
        ("Or", new Vector4(0.92f, 0.78f, 0.31f, 1f)),
        ("Rouge", new Vector4(0.75f, 0.25f, 0.22f, 1f)),
        ("Bleu", new Vector4(0.25f, 0.40f, 0.72f, 1f)),
        ("Vert", new Vector4(0.30f, 0.60f, 0.32f, 1f)),
        ("Violet", new Vector4(0.52f, 0.32f, 0.68f, 1f)),
        ("Gris", new Vector4(0.45f, 0.45f, 0.48f, 1f)),
    ];

    public static readonly string[] HairStyleNames = ["Court", "Long", "Crête"];

    public static readonly string[] AccessoryNames = ["Aucun", "Chapeau", "Bandeau"];
}
