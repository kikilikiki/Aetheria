using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Voir GDD/demande utilisateur — "Défis hebdomadaires" (contenu end-game) + défis mensuels, avec
/// une UI dédiée pour y accéder. Progression dérivée d'une statistique cumulative existante (voir
/// <c>StatisticsEntity</c>) moins un instantané pris au début de la période (voir
/// <see cref="ChallengeProgressEntity.BaselineValue"/>), plutôt qu'un second système de compteurs
/// à maintenir en parallèle.
/// </summary>
public sealed class ChallengeService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    private static string CurrentPeriodBucket(ChallengePeriod period)
    {
        var now = DateTime.UtcNow;
        if (period == ChallengePeriod.Monthly)
        {
            return $"{now.Year}-M{now.Month:00}";
        }

        var calendar = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        return $"{now.Year}-W{calendar.GetWeekOfYear(now, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday):00}";
    }

    private static long ReadStat(StatisticsEntity stats, ChallengeStatKind kind) => kind switch
    {
        ChallengeStatKind.MonstersCaptured => stats.Monsters.MonstersCaptured,
        ChallengeStatKind.ItemsCrafted => stats.Economy.ItemsCrafted,
        ChallengeStatKind.FightsWon => stats.Combat.FightsWon,
        ChallengeStatKind.PvpWins => stats.Pvp.Wins,
        _ => 0,
    };

    public async Task<List<ChallengeStatus>> GetStatusAsync(Guid characterId, CancellationToken ct = default)
    {
        var stats = await db.Statistics.FirstOrDefaultAsync(s => s.CharacterId == characterId, ct);
        if (stats is null)
        {
            return [];
        }

        var results = new List<ChallengeStatus>();
        foreach (var definition in ChallengeCatalog.All)
        {
            var progress = await GetOrCreateProgressAsync(characterId, definition, stats, ct);
            var currentValue = ReadStat(stats, definition.StatKind);
            var delta = Math.Max(0, currentValue - progress.BaselineValue);

            results.Add(new ChallengeStatus
            {
                Key = definition.Key,
                Name = definition.Name,
                Description = definition.Description,
                Period = definition.Period,
                Progress = Math.Min(delta, definition.TargetValue),
                TargetValue = definition.TargetValue,
                RewardGold = definition.RewardGold,
                IsCompleted = delta >= definition.TargetValue,
                IsClaimed = progress.IsClaimed,
            });
        }

        await db.SaveChangesAsync(ct);
        return results;
    }

    public async Task<ChallengeStatus> ClaimAsync(string sessionToken, Guid characterId, string challengeKey, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");

        var definition = ChallengeCatalog.Find(challengeKey)
            ?? throw new AccountOperationException("Défi introuvable.");

        var stats = await db.Statistics.FirstOrDefaultAsync(s => s.CharacterId == characterId, ct)
            ?? throw new AccountOperationException("Statistiques introuvables pour ce personnage.");

        var progress = await GetOrCreateProgressAsync(characterId, definition, stats, ct);
        var currentValue = ReadStat(stats, definition.StatKind);
        var delta = Math.Max(0, currentValue - progress.BaselineValue);

        if (delta < definition.TargetValue)
        {
            throw new AccountOperationException("Ce défi n'est pas encore terminé.");
        }

        if (progress.IsClaimed)
        {
            throw new AccountOperationException("Récompense déjà réclamée.");
        }

        progress.IsClaimed = true;
        character.Gold += definition.RewardGold;
        await db.SaveChangesAsync(ct);

        return new ChallengeStatus
        {
            Key = definition.Key,
            Name = definition.Name,
            Description = definition.Description,
            Period = definition.Period,
            Progress = Math.Min(delta, definition.TargetValue),
            TargetValue = definition.TargetValue,
            RewardGold = definition.RewardGold,
            IsCompleted = true,
            IsClaimed = true,
        };
    }

    private async Task<ChallengeProgressEntity> GetOrCreateProgressAsync(Guid characterId, ChallengeDefinition definition, StatisticsEntity stats, CancellationToken ct)
    {
        var periodBucket = CurrentPeriodBucket(definition.Period);
        var progress = await db.ChallengeProgress.FirstOrDefaultAsync(
            p => p.CharacterId == characterId && p.ChallengeKey == definition.Key && p.PeriodBucket == periodBucket, ct);

        if (progress is null)
        {
            progress = new ChallengeProgressEntity
            {
                Id = Guid.NewGuid(),
                CharacterId = characterId,
                ChallengeKey = definition.Key,
                PeriodBucket = periodBucket,
                BaselineValue = ReadStat(stats, definition.StatKind),
            };
            db.ChallengeProgress.Add(progress);
        }

        return progress;
    }
}
