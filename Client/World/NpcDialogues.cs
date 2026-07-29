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
        // Voir GDD/demande utilisateur — "une histoire avec des dialogues cohérents à suivre" :
        // fil narratif léger tissé avec la chaîne de quêtes (voir QuestCatalogSeeder) — des
        // créatures de plus en plus agressives sortent des donjons, le royaume manque de bras.
        ["Garde royal"] =
        [
            "Halte, voyageur. Je ne te reconnais pas.",
            "Les créatures qui sortent des donjons sont de plus en plus nombreuses,",
            "et de plus en plus agressives. Le royaume a besoin de gens capables.",
            "Si tu comptes rester, prouve ta valeur. Fais bon voyage.",
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
            "Avec ce qui rôde près des donjons ces temps-ci,",
            "tout le monde a besoin d'un bon équipement.",
        ],
        ["Villageois"] =
        [
            "La vie au village était paisible, avant.",
            "Maintenant on entend des bruits, la nuit, du côté du donjon...",
            "Le garde dit que ça vient de plus en plus près.",
        ],

        // PNJ d'intérieur (voir GDD — intérieurs de bâtiment enrichis), un par bâtiment,
        // voir BuildingInteriors.ForBuilding.
        ["Chambellan"] =
        [
            "Bienvenue au château, voyageur.",
            "Sa Majesté ne reçoit personne aujourd'hui — trop occupée",
            "à débattre de ce qu'il faut faire au sujet des donjons.",
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
