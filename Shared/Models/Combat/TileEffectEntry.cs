using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models.Combat;

/// <summary>Une case spéciale de la grille de combat (voir GDD/demande utilisateur — "pièges, cases destructibles, cases de lave, cases glacées").</summary>
public sealed record TileEffectEntry(int X, int Y, TileEffect Effect);
