using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>Création et adhésion aux guildes (voir <c>Docs/GameDesign.md</c> — section Guildes).</summary>
public sealed class GuildService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    public async Task<GuildSummary> CreateAsync(CreateGuildRequest request, CancellationToken ct = default)
    {
        var character = await ResolveOwnedCharacterAsync(request.SessionToken, request.CharacterId, ct);

        if (await db.GuildMembers.AnyAsync(m => m.CharacterId == character.Id, ct))
        {
            throw new AccountOperationException("Ce personnage appartient déjà à une guilde.");
        }

        if (await db.Guilds.AnyAsync(g => g.Name == request.Name, ct))
        {
            throw new AccountOperationException("Ce nom de guilde est déjà pris.");
        }

        var guild = new GuildEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            LeaderCharacterId = character.Id,
        };

        db.Guilds.Add(guild);
        db.GuildMembers.Add(new GuildMemberEntity { Id = Guid.NewGuid(), GuildId = guild.Id, CharacterId = character.Id });

        await db.SaveChangesAsync(ct);

        await new AchievementService(db).UnlockAsync(character.UserId, "fondateur_de_guilde", ct);

        return await BuildSummaryAsync(guild.Id, ct);
    }

    public async Task<GuildSummary> JoinAsync(Guid guildId, JoinGuildRequest request, CancellationToken ct = default)
    {
        var character = await ResolveOwnedCharacterAsync(request.SessionToken, request.CharacterId, ct);

        if (await db.GuildMembers.AnyAsync(m => m.CharacterId == character.Id, ct))
        {
            throw new AccountOperationException("Ce personnage appartient déjà à une guilde.");
        }

        var guild = await db.Guilds.FirstOrDefaultAsync(g => g.Id == guildId, ct)
            ?? throw new AccountOperationException("Guilde introuvable.");

        db.GuildMembers.Add(new GuildMemberEntity { Id = Guid.NewGuid(), GuildId = guild.Id, CharacterId = character.Id });
        await db.SaveChangesAsync(ct);

        return await BuildSummaryAsync(guild.Id, ct);
    }

    /// <summary>Guilde du personnage donné, ou <c>null</c> s'il n'en a pas (voir GDD — bouton Guilde en jeu).</summary>
    public async Task<GuildSummary?> GetForCharacterAsync(Guid characterId, CancellationToken ct = default)
    {
        var membership = await db.GuildMembers.FirstOrDefaultAsync(m => m.CharacterId == characterId, ct);
        return membership is null ? null : await BuildSummaryAsync(membership.GuildId, ct);
    }

    /// <summary>Recherche de guildes par nom (voir GDD — panneau Guilde : rejoindre/rechercher/créer). Toutes les guildes si <paramref name="search"/> est vide.</summary>
    public async Task<List<GuildSummary>> SearchAsync(string? search, CancellationToken ct = default)
    {
        var query = db.Guilds.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(g => g.Name.Contains(search));
        }

        var guildIds = await query.OrderBy(g => g.Name).Take(50).Select(g => g.Id).ToListAsync(ct);

        var summaries = new List<GuildSummary>();
        foreach (var id in guildIds)
        {
            summaries.Add(await BuildSummaryAsync(id, ct));
        }

        return summaries;
    }

    private const long ExperiencePerLevel = 1000;

    /// <summary>Voir GDD/demande utilisateur — "Quêtes de guilde".</summary>
    private const int WeeklyQuestItemTarget = 20;
    private const long WeeklyQuestRewardExperience = 2000;

    /// <summary>Voir GDD/demande utilisateur — "Guerres de guildes".</summary>
    private const long GuildWarWinnerRewardGold = 5000;

    private static string CurrentWeekBucket()
    {
        var now = DateTime.UtcNow;
        var calendar = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        return $"{now.Year}-W{calendar.GetWeekOfYear(now, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday):00}";
    }

    /// <summary>Voir GDD/demande utilisateur — "Guerres de guildes" : appelé après chaque victoire en duel amical (voir CombatService.ApplyPvpResultAsync/ApplyArenaResultAsync), sans effet si le personnage n'a pas de guilde.</summary>
    public async Task AwardWarPointsAsync(Guid characterId, long points, CancellationToken ct = default)
    {
        var membership = await db.GuildMembers.FirstOrDefaultAsync(m => m.CharacterId == characterId, ct);
        if (membership is null)
        {
            return;
        }

        var guild = await db.Guilds.FirstOrDefaultAsync(g => g.Id == membership.GuildId, ct);
        if (guild is null)
        {
            return;
        }

        guild.WarPoints += points;
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<GuildSummary>> GetWarStandingsAsync(int limit, CancellationToken ct = default)
    {
        var guildIds = await db.Guilds.OrderByDescending(g => g.WarPoints).Take(limit).Select(g => g.Id).ToListAsync(ct);
        var summaries = new List<GuildSummary>();
        foreach (var id in guildIds)
        {
            summaries.Add(await BuildSummaryAsync(id, ct));
        }

        return summaries;
    }

    /// <summary>Voir GDD/demande utilisateur — "Guerres de guildes" : la guilde en tête reçoit un bonus de trésor, resolu chaque semaine (voir KingdomWarScheduler).</summary>
    public async Task<string> ResolveWeeklyWarAsync(CancellationToken ct = default)
    {
        var guilds = await db.Guilds.Where(g => g.WarPoints > 0).OrderByDescending(g => g.WarPoints).ToListAsync(ct);
        if (guilds.Count == 0)
        {
            return "Aucune guilde n'a marqué de points de guerre cette semaine.";
        }

        var winner = guilds[0];
        winner.TreasuryGold += GuildWarWinnerRewardGold;

        foreach (var guild in guilds)
        {
            guild.WarPoints = 0;
        }

        await db.SaveChangesAsync(ct);
        return $"Guerre de guildes résolue — {winner.Name} l'emporte et reçoit {GuildWarWinnerRewardGold} or.";
    }

    /// <summary>Voir GDD/demande utilisateur — "Banque de guilde" et "Niveau de guilde" : chaque pièce déposée par un membre est aussi de l'XP de guilde, même formule que les autres systèmes de niveau (XP requise au niveau N = N × 1000).</summary>
    public async Task<GuildSummary> DepositGoldAsync(Guid guildId, GuildDepositGoldRequest request, CancellationToken ct = default)
    {
        var character = await ResolveOwnedCharacterAsync(request.SessionToken, request.CharacterId, ct);
        await RequireMembershipAsync(character.Id, guildId, ct);

        if (request.Amount <= 0 || character.Gold < request.Amount)
        {
            throw new AccountOperationException("Montant invalide ou pièces insuffisantes.");
        }

        var guild = await db.Guilds.FirstAsync(g => g.Id == guildId, ct);
        character.Gold -= request.Amount;
        guild.TreasuryGold += request.Amount;
        guild.GuildExperience += request.Amount;

        while (guild.GuildExperience >= guild.Level * ExperiencePerLevel)
        {
            guild.GuildExperience -= guild.Level * ExperiencePerLevel;
            guild.Level++;
        }

        await db.SaveChangesAsync(ct);
        return await BuildSummaryAsync(guild.Id, ct);
    }

    /// <summary>Voir GDD/demande utilisateur — "Coffre partagé".</summary>
    public async Task<List<GuildChestItemSummary>> GetChestAsync(Guid guildId, CancellationToken ct = default)
    {
        return await db.GuildChestItems
            .Where(i => i.GuildId == guildId)
            .Join(db.Items, i => i.ItemId, item => item.Id, (i, item) => new GuildChestItemSummary(i.ItemId, item.Name, i.Quantity))
            .ToListAsync(ct);
    }

    public async Task<List<GuildChestItemSummary>> DepositItemAsync(Guid guildId, GuildChestActionRequest request, CancellationToken ct = default)
    {
        var character = await ResolveOwnedCharacterAsync(request.SessionToken, request.CharacterId, ct);
        await RequireMembershipAsync(character.Id, guildId, ct);

        var inventoryItem = await db.InventoryItems.FirstOrDefaultAsync(
            i => i.CharacterId == character.Id && i.ItemId == request.ItemId && i.Quantity >= request.Quantity, ct)
            ?? throw new AccountOperationException("Vous ne possédez pas assez de cet objet.");

        inventoryItem.Quantity -= request.Quantity;
        if (inventoryItem.Quantity <= 0)
        {
            db.InventoryItems.Remove(inventoryItem);
        }

        var chestEntry = await db.GuildChestItems.FirstOrDefaultAsync(i => i.GuildId == guildId && i.ItemId == request.ItemId, ct);
        if (chestEntry is null)
        {
            chestEntry = new GuildChestItemEntity { Id = Guid.NewGuid(), GuildId = guildId, ItemId = request.ItemId };
            db.GuildChestItems.Add(chestEntry);
        }

        chestEntry.Quantity += request.Quantity;

        // Voir GDD/demande utilisateur — "Quêtes de guilde" : objectif hebdomadaire "déposer des
        // objets au coffre partagé", même mécanique de semaine ISO que KingdomWarScheduler.
        var guild = await db.Guilds.FirstAsync(g => g.Id == guildId, ct);
        var currentWeekBucket = CurrentWeekBucket();
        if (guild.WeeklyQuestWeekBucket != currentWeekBucket)
        {
            guild.WeeklyQuestWeekBucket = currentWeekBucket;
            guild.WeeklyQuestItemsDeposited = 0;
            guild.WeeklyQuestCompleted = false;
        }

        guild.WeeklyQuestItemsDeposited += request.Quantity;
        if (!guild.WeeklyQuestCompleted && guild.WeeklyQuestItemsDeposited >= WeeklyQuestItemTarget)
        {
            guild.WeeklyQuestCompleted = true;
            guild.GuildExperience += WeeklyQuestRewardExperience;
            while (guild.GuildExperience >= guild.Level * ExperiencePerLevel)
            {
                guild.GuildExperience -= guild.Level * ExperiencePerLevel;
                guild.Level++;
            }
        }

        await db.SaveChangesAsync(ct);
        return await GetChestAsync(guildId, ct);
    }

    public async Task<List<GuildChestItemSummary>> WithdrawItemAsync(Guid guildId, GuildChestActionRequest request, CancellationToken ct = default)
    {
        var character = await ResolveOwnedCharacterAsync(request.SessionToken, request.CharacterId, ct);
        await RequireMembershipAsync(character.Id, guildId, ct);

        var chestEntry = await db.GuildChestItems.FirstOrDefaultAsync(i => i.GuildId == guildId && i.ItemId == request.ItemId && i.Quantity >= request.Quantity, ct)
            ?? throw new AccountOperationException("Le coffre ne contient pas assez de cet objet.");

        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == request.ItemId, ct)
            ?? throw new AccountOperationException("Objet introuvable.");

        chestEntry.Quantity -= request.Quantity;
        if (chestEntry.Quantity <= 0)
        {
            db.GuildChestItems.Remove(chestEntry);
        }

        await InventoryStackingService.AddQuantityAsync(db, character.Id, request.ItemId, request.Quantity, item.MaxStackSize <= 0 ? 99 : item.MaxStackSize, ct);
        await db.SaveChangesAsync(ct);
        return await GetChestAsync(guildId, ct);
    }

    /// <summary>Voir GDD/demande utilisateur — "Classement" (des guildes) : niveau puis XP de guilde en départage.</summary>
    public async Task<List<GuildSummary>> GetLeaderboardAsync(int limit, CancellationToken ct = default)
    {
        var guildIds = await db.Guilds
            .OrderByDescending(g => g.Level)
            .ThenByDescending(g => g.GuildExperience)
            .Take(limit)
            .Select(g => g.Id)
            .ToListAsync(ct);

        var summaries = new List<GuildSummary>();
        foreach (var id in guildIds)
        {
            summaries.Add(await BuildSummaryAsync(id, ct));
        }

        return summaries;
    }

    private async Task RequireMembershipAsync(Guid characterId, Guid guildId, CancellationToken ct)
    {
        var isMember = await db.GuildMembers.AnyAsync(m => m.CharacterId == characterId && m.GuildId == guildId, ct);
        if (!isMember)
        {
            throw new AccountOperationException("Ce personnage n'appartient pas à cette guilde.");
        }
    }

    private async Task<CharacterEntity> ResolveOwnedCharacterAsync(string sessionToken, Guid characterId, CancellationToken ct)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.UserId == userId, ct);
        return character ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");
    }

    private async Task<GuildSummary> BuildSummaryAsync(Guid guildId, CancellationToken ct)
    {
        var guild = await db.Guilds.FirstAsync(g => g.Id == guildId, ct);
        var memberNames = await db.GuildMembers
            .Where(m => m.GuildId == guildId)
            .Join(db.Characters, m => m.CharacterId, c => c.Id, (m, c) => c.Name)
            .ToListAsync(ct);

        return new GuildSummary
        {
            Id = guild.Id,
            Name = guild.Name,
            Level = guild.Level,
            TreasuryGold = guild.TreasuryGold,
            LeaderCharacterId = guild.LeaderCharacterId,
            MemberNames = memberNames,
            GuildExperience = guild.GuildExperience,
            ExperienceForNextLevel = guild.Level * ExperiencePerLevel,
            WarPoints = guild.WarPoints,
            WeeklyQuestItemsDeposited = guild.WeeklyQuestWeekBucket == CurrentWeekBucket() ? guild.WeeklyQuestItemsDeposited : 0,
            WeeklyQuestItemTarget = WeeklyQuestItemTarget,
            WeeklyQuestCompleted = guild.WeeklyQuestWeekBucket == CurrentWeekBucket() && guild.WeeklyQuestCompleted,
        };
    }
}
