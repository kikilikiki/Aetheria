namespace Aetheria.Client.World;

/// <summary>
/// Répliques statiques par PNJ (voir <see cref="WorldMap.Npcs"/>). Pas d'arbre de dialogue ni de
/// conditions pour cette première version — une suite de lignes qu'on avance avec E, voir
/// <c>Docs/README.md</c> pour les évolutions prévues (quêtes, embranchements).
/// </summary>
public static class NpcDialogues
{
    public static readonly IReadOnlyDictionary<string, string[]> Lines = new Dictionary<string, string[]>
    {
        ["Garde royal"] =
        [
            "Halte, voyageur.",
            "La capitale est sous ma protection.",
            "Fais bon voyage, et prends garde aux donjons.",
        ],
        ["Marchande"] =
        [
            "Bienvenue a l'Hotel des ventes !",
            "J'ai les meilleurs prix du royaume.",
            "Reviens quand tu auras des objets a vendre.",
        ],
        ["Forgeron"] =
        [
            "Le feu de la forge ne s'eteint jamais.",
            "Apporte-moi du minerai, je t'en ferai une arme.",
        ],
        ["Villageois"] =
        [
            "La vie au village est paisible.",
            "On raconte que le donjon cache un tresor...",
        ],
    };
}
