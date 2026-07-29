using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.Persistence;

/// <summary>
/// Voir GDD/demande utilisateur — "un tutoriel qui force le joueur à faire des quêtes qui lui
/// explique le jeu" et "une histoire avec des dialogues cohérents à suivre". Chaîne linéaire
/// courte (voir QuestEntity.SequenceOrder) : chaque étape enseigne une mécanique du jeu tout en
/// faisant avancer un fil narratif simple — le royaume est troublé par des créatures de plus en
/// plus agressives sorties des donjons, le joueur (nouvel arrivant) doit faire ses preuves.
/// **Simplification assumée** : pas d'embranchements ni de choix, une seule quête "active" à la
/// fois — voir Docs/README.md pour les évolutions prévues.
/// </summary>
public static class QuestCatalogSeeder
{
    public static async Task SeedAsync(AetheriaDbContext db, CancellationToken ct = default)
    {
        if (await db.Quests.AnyAsync(ct))
        {
            return;
        }

        db.Quests.AddRange(
            new QuestEntity
            {
                SequenceOrder = 1,
                Name = "Une arrivée remarquée",
                Description = "Le Garde royal semble méfiant. Va lui parler pour te présenter.",
                RewardGold = 20,
            },
            new QuestEntity
            {
                SequenceOrder = 2,
                Name = "Faire ses preuves",
                Description = "Des créatures rôdent aux abords de la capitale. Remporte ton premier combat.",
                RewardGold = 30,
                RewardExperience = 20,
            },
            new QuestEntity
            {
                SequenceOrder = 3,
                Name = "Un allié à quatre pattes",
                Description = "Une créature affaiblie ne demande qu'à te suivre. Capture-en une avec une Carte de capture.",
                RewardGold = 25,
            },
            new QuestEntity
            {
                SequenceOrder = 4,
                Name = "Le forgeron a besoin de bras",
                Description = "Rends-toi à la Forge, parle à l'Apprenti forgeron et fabrique ton premier objet.",
                RewardGold = 25,
            },
            new QuestEntity
            {
                SequenceOrder = 5,
                Name = "Les rouages du commerce",
                Description = "La Marchande t'apprendra à acheter et vendre. Fais affaire avec elle une première fois.",
                RewardGold = 20,
            },
            new QuestEntity
            {
                SequenceOrder = 6,
                Name = "Les échos du donjon",
                Description = "Des bruits inquiétants viennent du donjon voisin. Vas-y voir de plus près.",
                RewardGold = 50,
                RewardExperience = 40,
            });

        await db.SaveChangesAsync(ct);
    }
}
