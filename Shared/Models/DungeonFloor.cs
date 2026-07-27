using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>Une salle d'étage de donjon et son contenu.</summary>
public sealed record DungeonRoom(int Index, DungeonEncounterType EncounterType);

/// <summary>
/// Le contenu généré d'un étage : ses salles, dans l'ordre de traversée. Produit par
/// <c>Server/World/DungeonFloorGenerator</c>, affiché tel quel par le Client et le MapEditor.
/// </summary>
public sealed record DungeonFloor(int FloorNumber, IReadOnlyList<DungeonRoom> Rooms);
