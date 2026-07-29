namespace Aetheria.Shared.Enums;

/// <summary>Catégorie d'un objet d'inventaire.</summary>
public enum ItemType
{
    Arme,
    Armure,
    Consommable,
    Ressource,
    ObjetDeCapture,
    Monture,
    Cosmetique,
    QuestObjet,

    /// <summary>Voir GDD/demande utilisateur — anneaux/colliers/capes, équipables comme les armes/armures.</summary>
    Accessoire,
}
