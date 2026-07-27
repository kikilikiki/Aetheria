namespace Aetheria.Shared.Enums;

/// <summary>
/// Contenu d'une salle de donjon généré procéduralement (voir <c>Docs/GameDesign.md</c> —
/// section Donjons).
/// </summary>
public enum DungeonEncounterType
{
    Monstre,
    Evenement,
    Enigme,
    Coffre,
    Piege,
    Marchand,
    SalleSecrete,
    Autel,
    MiniBoss,
    Boss,
    BossLegendaire,
}
