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
        };
    }
}
