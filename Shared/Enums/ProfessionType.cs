namespace Aetheria.Shared.Enums;

/// <summary>Métier exerçable par un personnage (voir <c>Docs/GameDesign.md</c> — section Métiers).</summary>
public enum ProfessionType
{
    Mineur,
    Forgeron,
    Alchimiste,
    Chasseur,
    Cuisinier,
    Enchanteur,
    Artisan,

    /// <summary>Voir GDD/demande utilisateur — "ajoute des bâtiments dans les villes (mine, champs etc) pour avoir des objets" : récolte au Champ (voir WorldMap, "Champ").</summary>
    Agriculteur,
}
