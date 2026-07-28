using System.Numerics;
using Aetheria.Shared.Enums;

namespace Aetheria.Client.World;

/// <summary>
/// Palette visuelle et noms propres à un royaume (voir GDD — "plusieurs villes distinctes par
/// royaume/biome"). Chaque royaume a sa propre capitale, avec un nom et des couleurs de terrain
/// distincts, pour qu'un personnage du Royaume de Feu et un personnage du Royaume des Glaces
/// n'atterrissent visiblement pas au même endroit. **Portée assumée** : un joueur ne voit que la
/// capitale de son propre royaume (celui choisi à la création du personnage, voir
/// <c>CharacterSummary.Kingdom</c>) — il n'y a pas encore de voyage entre royaumes ni de carte du
/// monde reliant les quatre villes entre elles (voir Docs/README.md).
/// </summary>
public sealed record KingdomBiome(
    string CapitalName,
    string DungeonName,
    Vector4 GrassLight,
    Vector4 GrassMid,
    Vector4 GrassDark,
    Vector4 GroundPath,
    Vector4 Water,
    Vector4 AccentTint)
{
    public static KingdomBiome For(KingdomType kingdom) => kingdom switch
    {
        KingdomType.Feu => new KingdomBiome(
            CapitalName: "Citadelle de Braise",
            DungeonName: "Gouffre Ardent",
            GrassLight: new Vector4(0.42f, 0.24f, 0.14f, 1f),
            GrassMid: new Vector4(0.36f, 0.19f, 0.11f, 1f),
            GrassDark: new Vector4(0.28f, 0.14f, 0.08f, 1f),
            GroundPath: new Vector4(0.55f, 0.30f, 0.15f, 1f),
            Water: new Vector4(0.65f, 0.28f, 0.10f, 1f),
            AccentTint: new Vector4(1.08f, 0.85f, 0.72f, 1f)),

        KingdomType.Glaces => new KingdomBiome(
            CapitalName: "Citadelle de Glace",
            DungeonName: "Crevasse Gelée",
            GrassLight: new Vector4(0.78f, 0.85f, 0.90f, 1f),
            GrassMid: new Vector4(0.68f, 0.78f, 0.85f, 1f),
            GrassDark: new Vector4(0.58f, 0.70f, 0.80f, 1f),
            GroundPath: new Vector4(0.55f, 0.58f, 0.62f, 1f),
            Water: new Vector4(0.45f, 0.65f, 0.80f, 1f),
            AccentTint: new Vector4(0.85f, 0.95f, 1.08f, 1f)),

        KingdomType.Ombres => new KingdomBiome(
            CapitalName: "Bastion des Ombres",
            DungeonName: "Antre des Ténèbres",
            GrassLight: new Vector4(0.28f, 0.24f, 0.34f, 1f),
            GrassMid: new Vector4(0.22f, 0.19f, 0.28f, 1f),
            GrassDark: new Vector4(0.16f, 0.14f, 0.22f, 1f),
            GroundPath: new Vector4(0.32f, 0.28f, 0.38f, 1f),
            Water: new Vector4(0.15f, 0.10f, 0.22f, 1f),
            AccentTint: new Vector4(0.85f, 0.78f, 1.05f, 1f)),

        // Nature (par défaut) : la carte de démonstration d'origine.
        _ => new KingdomBiome(
            CapitalName: "Sylvaltar",
            DungeonName: "Donjon des Araignées",
            GrassLight: new Vector4(0.35f, 0.55f, 0.28f, 1f),
            GrassMid: new Vector4(0.30f, 0.48f, 0.24f, 1f),
            GrassDark: new Vector4(0.25f, 0.42f, 0.20f, 1f),
            GroundPath: new Vector4(0.55f, 0.44f, 0.30f, 1f),
            Water: new Vector4(0.20f, 0.40f, 0.65f, 1f),
            AccentTint: Vector4.One),
    };
}
