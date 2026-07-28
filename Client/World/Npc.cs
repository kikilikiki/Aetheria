using System.Numerics;

namespace Aetheria.Client.World;

/// <summary>
/// Un personnage non-joueur statique posé sur la carte (voir <c>Docs/README.md</c> pour les
/// limites assumées : pas de dialogue ni d'IA de déplacement pour cette première version,
/// juste une présence visuelle avec une légère animation d'attente).
/// </summary>
public sealed record Npc(string Name, int GridX, int GridY, Vector4 BodyColor, Vector4 HeadColor, float AnimationOffset);
