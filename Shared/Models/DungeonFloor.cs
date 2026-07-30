using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>
/// Une salle d'étage de donjon et son contenu. Voir GDD/demande utilisateur — "les donjons
/// doivent être comme The Binding of Isaac : des salles aléatoires avec coffre/monstre etc, mais
/// où on se déplace nous-même de salle en salle" : <see cref="GridX"/>/<see cref="GridY"/>
/// positionnent la salle sur la grille de l'étage (voir <c>DungeonFloorGenerator</c> — marche
/// aléatoire depuis la salle de départ), <see cref="North"/>/<see cref="South"/>/<see cref="East"/>/
/// <see cref="West"/> indiquent où se trouvent les portes (= une salle voisine existe dans cette
/// direction). <see cref="Index"/> reste l'identifiant utilisé par les appels serveur existants
/// (combat, coffre) — inchangé, seule la disposition spatiale est nouvelle.
/// </summary>
public sealed record DungeonRoom(
    int Index,
    DungeonEncounterType EncounterType,
    int GridX = 0,
    int GridY = 0,
    bool North = false,
    bool South = false,
    bool East = false,
    bool West = false,
    bool IsStart = false);

/// <summary>
/// Le contenu généré d'un étage : ses salles, positionnées sur une grille (voir
/// <see cref="DungeonRoom"/>). Produit par <c>Server/World/DungeonFloorGenerator</c>, affiché tel
/// quel par le Client et le MapEditor.
/// </summary>
public sealed record DungeonFloor(int FloorNumber, IReadOnlyList<DungeonRoom> Rooms);

/// <summary>Voir retour utilisateur — "plafonne à 10 étages, mini boss à 3, boss à 5 et boss légendaire à 10" : borne partagée entre <c>Server/World/DungeonFloorGenerator</c> (génération) et le Client (boucle de parcours, voir UpdateDungeonCorridor).</summary>
public static class DungeonProgression
{
    public const int MaxFloor = 10;
}
