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

        // PNJ d'intérieur (voir GDD — intérieurs de bâtiment enrichis), un par bâtiment,
        // voir BuildingInteriors.ForBuilding.
        ["Chambellan"] =
        [
            "Bienvenue au château, voyageur.",
            "Sa Majesté ne reçoit personne aujourd'hui.",
        ],
        ["Aubergiste"] =
        [
            "Assieds-toi, la soupe est chaude.",
            "Les chambres sont à l'étage, si le cœur t'en dit.",
        ],
        ["Commis"] =
        [
            "L'Hôtel des ventes n'a jamais été aussi actif.",
            "Reviens voir le catalogue régulièrement.",
        ],
        ["Apprenti forgeron"] =
        [
            "Le maître forgeron est occupé avec l'enclume.",
            "Reviens plus tard, il te fera peut-être une arme.",
        ],
        ["Archiviste"] =
        [
            "Toutes les guildes du royaume sont répertoriées ici.",
            "Fonde la tienne, et ton nom y figurera aussi.",
        ],
    };
}
