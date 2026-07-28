using System.Numerics;

namespace Aetheria.Client.World;

/// <summary>
/// Meuble décoratif dans une scène d'intérieur (voir GDD — intérieurs enrichis). Un simple
/// rectangle coloré positionné en coordonnées relatives (0..1 de la largeur/hauteur de l'écran),
/// pas un vrai objet 3D/isométrique — cohérent avec le style écran-plat déjà utilisé par
/// <c>DrawInteriorScene</c> (voir Docs/README.md pour cette limite assumée).
/// </summary>
public sealed record InteriorFurniture(string Label, float RelativeX, float RelativeY, float RelativeWidth, float RelativeHeight, Vector4 Color);

/// <summary>PNJ présent à l'intérieur d'un bâtiment (voir GDD), avec ses répliques dans <see cref="NpcDialogues"/>.</summary>
public sealed record InteriorNpc(string Name, Vector4 BodyColor, Vector4 HeadColor);

public sealed record BuildingInteriorLayout(IReadOnlyList<InteriorFurniture> Furniture, IReadOnlyList<InteriorNpc> Npcs);

/// <summary>Agencement d'intérieur par nom de bâtiment (voir <see cref="WorldMap.Buildings"/>).</summary>
public static class BuildingInteriors
{
    private static readonly Vector4 WoodDark = new(0.42f, 0.30f, 0.20f, 1f);
    private static readonly Vector4 WoodMid = new(0.52f, 0.38f, 0.24f, 1f);
    private static readonly Vector4 StoneGray = new(0.30f, 0.30f, 0.33f, 1f);
    private static readonly Vector4 EmberOrange = new(0.75f, 0.32f, 0.14f, 1f);

    public static BuildingInteriorLayout ForBuilding(string name) => name switch
    {
        "Capitale" =>
            new BuildingInteriorLayout(
                [
                    new InteriorFurniture("Trône", 0.44f, 0.58f, 0.14f, 0.24f, new Vector4(0.75f, 0.62f, 0.20f, 1f)),
                    new InteriorFurniture("Tapis", 0.30f, 0.86f, 0.42f, 0.05f, new Vector4(0.55f, 0.12f, 0.14f, 1f)),
                ],
                [new InteriorNpc("Chambellan", new Vector4(0.30f, 0.25f, 0.45f, 1f), new Vector4(0.85f, 0.70f, 0.55f, 1f))]),

        "Village" =>
            new BuildingInteriorLayout(
                [
                    new InteriorFurniture("Table", 0.40f, 0.68f, 0.20f, 0.10f, WoodDark),
                    new InteriorFurniture("Âtre", 0.74f, 0.52f, 0.13f, 0.30f, new Vector4(0.35f, 0.22f, 0.16f, 1f)),
                ],
                [new InteriorNpc("Aubergiste", new Vector4(0.42f, 0.32f, 0.20f, 1f), new Vector4(0.88f, 0.72f, 0.58f, 1f))]),

        "Hôtel des ventes" =>
            new BuildingInteriorLayout(
                [
                    new InteriorFurniture("Comptoir", 0.28f, 0.62f, 0.42f, 0.10f, WoodDark),
                    new InteriorFurniture("Étagères", 0.76f, 0.38f, 0.13f, 0.46f, WoodMid),
                ],
                [new InteriorNpc("Commis", new Vector4(0.20f, 0.42f, 0.35f, 1f), new Vector4(0.88f, 0.72f, 0.58f, 1f))]),

        "Forge" =>
            new BuildingInteriorLayout(
                [
                    new InteriorFurniture("Enclume", 0.42f, 0.66f, 0.15f, 0.11f, StoneGray),
                    new InteriorFurniture("Fournaise", 0.74f, 0.44f, 0.15f, 0.36f, EmberOrange),
                ],
                [new InteriorNpc("Apprenti forgeron", new Vector4(0.35f, 0.30f, 0.30f, 1f), new Vector4(0.82f, 0.63f, 0.48f, 1f))]),

        "Guilde" =>
            new BuildingInteriorLayout(
                [
                    new InteriorFurniture("Bannières", 0.14f, 0.28f, 0.11f, 0.42f, new Vector4(0.45f, 0.25f, 0.55f, 1f)),
                    new InteriorFurniture("Table des quêtes", 0.38f, 0.68f, 0.28f, 0.10f, new Vector4(0.40f, 0.28f, 0.20f, 1f)),
                ],
                [new InteriorNpc("Archiviste", new Vector4(0.55f, 0.35f, 0.65f, 1f), new Vector4(0.85f, 0.70f, 0.60f, 1f))]),

        _ => new BuildingInteriorLayout([], []),
    };
}
