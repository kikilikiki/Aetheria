using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Voir GDD/demande utilisateur — "un tutoriel qui force le joueur à faire des quêtes" : une
/// seule quête "active" à la fois par personnage (la première non complétée par
/// <see cref="QuestEntity.SequenceOrder"/>), voir <see cref="QuestCatalogSeeder"/>.
/// </summary>
public sealed class QuestService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    public async Task<QuestSummary?> GetActiveQuestAsync(Guid characterId, CancellationToken ct = default)
    {
        var completedQuestIds = await db.CharacterQuestProgress
            .Where(p => p.CharacterId == characterId && p.IsCompleted)
            .Select(p => p.QuestId)
            .ToListAsync(ct);

        // Catalogue de quelques quêtes seulement (voir QuestCatalogSeeder) — chargé en entier une
        // fois, moins cher que plusieurs allers-retours pour résoudre un embranchement éventuel.
        var allQuests = await db.Quests.ToListAsync(ct);

        // Voir Docs/Idees.md — "Embranchements/choix dans la chaîne de quêtes tutoriel" : un
        // embranchement est en attente si une quête complétée propose un choix
        // (QuestEntity.ChoiceNextQuestId) et qu'aucune des deux options n'a encore été résolue.
        var pendingChoiceSource = allQuests
            .Where(q => completedQuestIds.Contains(q.Id) && q.ChoiceNextQuestId is not null)
            .Select(q => new
            {
                Source = q,
                OptionA = allQuests.FirstOrDefault(n => n.SequenceOrder == q.SequenceOrder + 1 && n.Id != q.ChoiceNextQuestId),
                OptionB = allQuests.FirstOrDefault(n => n.Id == q.ChoiceNextQuestId),
            })
            .Where(x => x.OptionA is not null && x.OptionB is not null
                && !completedQuestIds.Contains(x.OptionA.Id) && !completedQuestIds.Contains(x.OptionB.Id))
            .OrderByDescending(x => x.Source.SequenceOrder)
            .FirstOrDefault();

        if (pendingChoiceSource is not null)
        {
            var optionA = pendingChoiceSource.OptionA!;
            var optionB = pendingChoiceSource.OptionB!;
            return new QuestSummary
            {
                Id = -1,
                Name = "Un choix à faire",
                Description = "Deux voies s'offrent à toi. Choisis celle que tu veux suivre.",
                IsChoice = true,
                ChoiceOptionAId = optionA.Id,
                ChoiceOptionAName = optionA.Name,
                ChoiceOptionBId = optionB.Id,
                ChoiceOptionBName = optionB.Name,
            };
        }

        var quest = allQuests
            .Where(q => !completedQuestIds.Contains(q.Id))
            .OrderBy(q => q.SequenceOrder)
            .FirstOrDefault();

        return quest is null ? null : new QuestSummary { Id = quest.Id, Name = quest.Name, Description = quest.Description };
    }

    /// <summary>
    /// Résout un embranchement en attente (voir <see cref="GetActiveQuestAsync"/>) : marque
    /// l'option NON choisie comme complétée sans récompense (pour l'exclure définitivement de la
    /// suite), ce qui laisse l'option choisie redevenir la quête active au prochain appel.
    /// Ignore silencieusement si aucun embranchement n'est réellement en attente ou si
    /// <paramref name="chosenQuestId"/> ne correspond à aucune des deux options — mêmes garanties
    /// défensives que <see cref="CompleteIfActiveAsync"/>.
    /// </summary>
    public async Task ChooseNextQuestAsync(string sessionToken, Guid characterId, int chosenQuestId, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            return;
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.UserId == userId, ct);
        if (character is null)
        {
            return;
        }

        var active = await GetActiveQuestAsync(characterId, ct);
        if (active is not { IsChoice: true })
        {
            return;
        }

        int rejectedQuestId;
        if (chosenQuestId == active.ChoiceOptionAId)
        {
            rejectedQuestId = active.ChoiceOptionBId!.Value;
        }
        else if (chosenQuestId == active.ChoiceOptionBId)
        {
            rejectedQuestId = active.ChoiceOptionAId!.Value;
        }
        else
        {
            return;
        }

        db.CharacterQuestProgress.Add(new CharacterQuestProgressEntity
        {
            Id = Guid.NewGuid(),
            CharacterId = characterId,
            QuestId = rejectedQuestId,
            IsCompleted = true,
            CompletedAtUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Marque la quête <paramref name="questName"/> comme complétée pour ce personnage si elle
    /// est bien l'étape courante (ignore silencieusement sinon — évite qu'une action répétée
    /// après complétion ne redonne la récompense ou ne fasse planter l'appelant). Identifiée par
    /// nom plutôt que par id : les points d'ancrage côté client (fin de dialogue, victoire en
    /// combat, capture, craft, achat/vente, entrée en donjon) connaissent le nom de la quête
    /// concernée, pas un id de catalogue arbitraire.
    /// </summary>
    public async Task CompleteIfActiveAsync(string sessionToken, Guid characterId, string questName, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            return;
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.UserId == userId, ct);
        if (character is null)
        {
            return;
        }

        var active = await GetActiveQuestAsync(characterId, ct);
        if (active is null || active.Name != questName)
        {
            return;
        }

        var quest = await db.Quests.FirstAsync(q => q.Id == active.Id, ct);

        db.CharacterQuestProgress.Add(new CharacterQuestProgressEntity
        {
            Id = Guid.NewGuid(),
            CharacterId = characterId,
            QuestId = quest.Id,
            IsCompleted = true,
            CompletedAtUtc = DateTime.UtcNow,
        });

        // Voir GDD/demande utilisateur — "grade payant... 0.1%/0.2%/0.3% de gain d'xp et d'argent
        // en plus" (voir PremiumService) et "consommables pour booster... l'xp la money" (voir
        // TemporaryBoostService) : les deux se cumulent.
        var gradeMultiplier = await PremiumService.GetXpGoldMultiplierAsync(db, userId, ct);
        character.Gold += (long)Math.Round(quest.RewardGold * gradeMultiplier * TemporaryBoostService.GoldMultiplier(character));
        var xpGained = (long)Math.Round(quest.RewardExperience * gradeMultiplier * TemporaryBoostService.XpMultiplier(character));
        CharacterProgressionService.GrantExperience(character, xpGained);
        await BattlePassService.GrantExperienceAsync(db, character, xpGained, ct);
        await db.SaveChangesAsync(ct);
    }
}
