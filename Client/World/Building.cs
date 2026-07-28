using System.Numerics;

namespace Aetheria.Client.World;

/// <summary>
/// Un bâtiment posé sur une case de la carte, dessiné en pseudo-3D (toit + deux murs) pour
/// renforcer l'effet de perspective. Empreinte d'une seule case pour cette première version —
/// des bâtiments à l'échelle réelle (plusieurs cases) sont un travail futur (voir Docs/README.md).
/// </summary>
public sealed record Building(
    string Name,
    int GridX,
    int GridY,
    float Height,
    Vector4 RoofColor,
    Vector4 WallColorLeft,
    Vector4 WallColorRight);
