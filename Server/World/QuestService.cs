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

        var quest = await db.Quests
            .Where(q => !completedQuestIds.Contains(q.Id))
            .OrderBy(q => q.SequenceOrder)
            .FirstOrDefaultAsync(ct);

        return quest is null ? null : new QuestSummary { Id = quest.Id, Name = quest.Name, Description = quest.Description };
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

        character.Gold += quest.RewardGold;
        CharacterProgressionService.GrantExperience(character, quest.RewardExperience);
        await db.SaveChangesAsync(ct);
    }
}
