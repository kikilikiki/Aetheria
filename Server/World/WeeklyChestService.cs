using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Voir GDD/demande utilisateur — "Exploration : coffres cachés hebdomadaires par royaume" : un
/// coffre par royaume, réclamable une fois par semaine par le premier personnage de ce royaume à
/// le faire (voir <see cref="ClaimAsync"/>). Créé à la demande (voir <see cref="GetOrCreateAsync"/>)
/// plutôt que par un job de seed, même clé de semaine ISO que <c>KingdomWarScheduler</c>.
/// </summary>
public sealed class WeeklyChestService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    private const long RewardGold = 500;

    private static string CurrentWeekBucket()
    {
        var now = DateTime.UtcNow;
        var calendar = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        return $"{now.Year}-W{calendar.GetWeekOfYear(now, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday):00}";
    }

    private async Task<WeeklyChestEntity> GetOrCreateAsync(int kingdomId, CancellationToken ct)
    {
        var weekBucket = CurrentWeekBucket();
        var chest = await db.WeeklyChests.FirstOrDefaultAsync(c => c.KingdomId == kingdomId && c.WeekBucket == weekBucket, ct);
        if (chest is null)
        {
            chest = new WeeklyChestEntity { Id = Guid.NewGuid(), KingdomId = kingdomId, WeekBucket = weekBucket, RewardGold = RewardGold };
            db.WeeklyChests.Add(chest);
            await db.SaveChangesAsync(ct);
        }

        return chest;
    }

    public async Task<WeeklyChestStatus> GetStatusAsync(int kingdomId, CancellationToken ct = default)
    {
        var chest = await GetOrCreateAsync(kingdomId, ct);
        return ToStatus(chest);
    }

    public async Task<WeeklyChestStatus> ClaimAsync(string sessionToken, Guid characterId, int kingdomId, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");

        var kingdom = await db.Kingdoms.FirstOrDefaultAsync(k => k.Id == kingdomId, ct)
            ?? throw new AccountOperationException("Royaume introuvable.");

        if (character.Kingdom != kingdom.Type)
        {
            throw new AccountOperationException("Ce coffre appartient à un autre royaume.");
        }

        var chest = await GetOrCreateAsync(kingdomId, ct);
        if (chest.ClaimedByCharacterId is not null)
        {
            throw new AccountOperationException($"Déjà réclamé cette semaine par {chest.ClaimedByCharacterName}.");
        }

        chest.ClaimedByCharacterId = character.Id;
        chest.ClaimedByCharacterName = character.Name;
        chest.ClaimedAtUtc = DateTime.UtcNow;

        var netGold = await KingdomPoliticsService.ApplyTaxAsync(db, character, chest.RewardGold, ct);
        character.Gold += netGold;

        await db.SaveChangesAsync(ct);
        return ToStatus(chest);
    }

    private static WeeklyChestStatus ToStatus(WeeklyChestEntity chest) => new()
    {
        WeekBucket = chest.WeekBucket,
        IsClaimed = chest.ClaimedByCharacterId is not null,
        ClaimedByCharacterName = chest.ClaimedByCharacterName,
        RewardGold = chest.RewardGold,
    };
}
