using System.Collections.Concurrent;
using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Models.GuildRaid;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Voir GDD/demande utilisateur — "Raids de guilde (boss coopératif nécessitant plusieurs
/// joueurs, distinct du world boss solo/petit groupe)". Même mécanique que
/// <see cref="WorldBossService"/> (attaque directe par bouton + cooldown à partir de l'équipe
/// active, pas raccroché au moteur de combat tactique) mais scopée à une guilde : un seul raid
/// actif par guilde à la fois, invocable par n'importe quel membre en dépensant l'or de la
/// banque de guilde (voir GuildEntity.TreasuryGold — première dépense de cette banque, jusqu'ici
/// jamais consommée), avec un cooldown de 12h entre deux invocations pour éviter le spam.
/// </summary>
public sealed class GuildRaidService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    private static readonly ConcurrentDictionary<Guid, DateTime> LastAttackAtUtc = new();
    private static readonly TimeSpan AttackCooldown = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan SpawnCooldown = TimeSpan.FromHours(12);

    public async Task<GuildRaidStatus> SpawnAsync(GuildRaidSpawnRequest request, CancellationToken ct = default)
    {
        var (character, guild) = await ResolveGuildMemberAsync(request.SessionToken, request.CharacterId, ct);

        var lastRaid = await db.GuildRaids.Where(r => r.GuildId == guild.Id).OrderByDescending(r => r.SpawnedAtUtc).FirstOrDefaultAsync(ct);
        if (lastRaid is { IsAlive: true })
        {
            throw new AccountOperationException("Un raid de guilde est déjà en cours.");
        }

        if (lastRaid is not null && DateTime.UtcNow - lastRaid.SpawnedAtUtc < SpawnCooldown)
        {
            var remaining = SpawnCooldown - (DateTime.UtcNow - lastRaid.SpawnedAtUtc);
            throw new AccountOperationException($"Il faut encore attendre {Math.Ceiling(remaining.TotalHours)}h avant d'invoquer un nouveau raid.");
        }

        var cost = 500L + guild.Level * 100L;
        if (guild.TreasuryGold < cost)
        {
            throw new AccountOperationException($"La banque de guilde n'a pas assez d'or ({guild.TreasuryGold}/{cost} requis).");
        }

        var allSpecies = await db.MonsterSpecies.ToListAsync(ct);
        if (allSpecies.Count == 0)
        {
            throw new AccountOperationException("Aucune espèce disponible pour ce raid.");
        }

        var species = allSpecies[Random.Shared.Next(allSpecies.Count)];
        guild.TreasuryGold -= cost;

        var maxHealth = 400 + guild.Level * 250;
        var raid = new GuildRaidEntity
        {
            Id = Guid.NewGuid(),
            GuildId = guild.Id,
            Name = species.Name,
            SpeciesId = species.Id,
            BossElement = species.Element,
            MaxHealth = maxHealth,
            CurrentHealth = maxHealth,
        };

        db.GuildRaids.Add(raid);
        await db.SaveChangesAsync(ct);

        return ToStatus(raid);
    }

    public async Task<GuildRaidStatus?> GetStatusAsync(Guid characterId, CancellationToken ct = default)
    {
        var membership = await db.GuildMembers.FirstOrDefaultAsync(m => m.CharacterId == characterId, ct);
        if (membership is null)
        {
            return null;
        }

        var raid = await db.GuildRaids.Where(r => r.GuildId == membership.GuildId).OrderByDescending(r => r.SpawnedAtUtc).FirstOrDefaultAsync(ct);
        return raid is null ? null : ToStatus(raid);
    }

    public async Task<GuildRaidAttackResponse> AttackAsync(GuildRaidAttackRequest request, CancellationToken ct = default)
    {
        var (character, guild) = await ResolveGuildMemberAsync(request.SessionToken, request.CharacterId, ct);

        var raid = await db.GuildRaids.Where(r => r.GuildId == guild.Id && r.IsAlive).OrderByDescending(r => r.SpawnedAtUtc).FirstOrDefaultAsync(ct);
        if (raid is null)
        {
            throw new AccountOperationException("Aucun raid de guilde actif pour le moment.");
        }

        if (LastAttackAtUtc.TryGetValue(character.Id, out var last) && DateTime.UtcNow - last < AttackCooldown)
        {
            var remaining = AttackCooldown - (DateTime.UtcNow - last);
            throw new AccountOperationException($"Il faut encore attendre {Math.Ceiling(remaining.TotalSeconds)}s avant d'attaquer à nouveau.");
        }

        LastAttackAtUtc[character.Id] = DateTime.UtcNow;

        var damage = await ComputeDamageAsync(character, ct);
        raid.CurrentHealth = Math.Max(0, raid.CurrentHealth - damage);

        var entry = await db.GuildRaidDamageEntries.FirstOrDefaultAsync(e => e.GuildRaidId == raid.Id && e.CharacterId == character.Id, ct);
        if (entry is null)
        {
            entry = new GuildRaidDamageEntity { Id = Guid.NewGuid(), GuildRaidId = raid.Id, CharacterId = character.Id, CharacterName = character.Name };
            db.GuildRaidDamageEntries.Add(entry);
        }

        entry.TotalDamage += damage;

        var bossKilled = false;
        if (raid.CurrentHealth <= 0 && raid.IsAlive)
        {
            raid.IsAlive = false;
            raid.KilledAtUtc = DateTime.UtcNow;
            raid.KillerCharacterName = character.Name;
            bossKilled = true;

            // Voir GDD/demande utilisateur — "boss coopératif" : récompense individuelle
            // proportionnelle aux dégâts (même mécanique que WorldBossService.AttackAsync), plus
            // un bonus commun à la banque de guilde pour renforcer la coopération.
            var participants = await db.GuildRaidDamageEntries.Where(e => e.GuildRaidId == raid.Id).ToListAsync(ct);
            var participantCharacterIds = participants.Select(p => p.CharacterId).ToList();
            var participantCharacters = await db.Characters.Where(c => participantCharacterIds.Contains(c.Id)).ToListAsync(ct);
            foreach (var participant in participants)
            {
                var participantCharacter = participantCharacters.FirstOrDefault(c => c.Id == participant.CharacterId);
                if (participantCharacter is not null)
                {
                    participantCharacter.Gold += participant.TotalDamage * 2L;
                }
            }

            guild.TreasuryGold += 1000L + guild.Level * 100L;
            guild.GuildExperience += 500L;
        }

        await db.SaveChangesAsync(ct);

        var message = bossKilled
            ? $"{raid.Name} a été vaincu par {character.Name} ! Récompenses distribuées à toute la guilde."
            : $"{damage} dégâts infligés à {raid.Name} ({raid.CurrentHealth}/{raid.MaxHealth} PV restants).";

        return new GuildRaidAttackResponse(true, message, damage, entry.TotalDamage, bossKilled, raid.CurrentHealth);
    }

    public async Task<List<GuildRaidLeaderboardRow>> GetLeaderboardAsync(Guid characterId, int limit, CancellationToken ct = default)
    {
        var membership = await db.GuildMembers.FirstOrDefaultAsync(m => m.CharacterId == characterId, ct);
        if (membership is null)
        {
            return [];
        }

        var raid = await db.GuildRaids.Where(r => r.GuildId == membership.GuildId).OrderByDescending(r => r.SpawnedAtUtc).FirstOrDefaultAsync(ct);
        if (raid is null)
        {
            return [];
        }

        return await db.GuildRaidDamageEntries
            .Where(e => e.GuildRaidId == raid.Id)
            .OrderByDescending(e => e.TotalDamage)
            .Take(limit)
            .Select(e => new GuildRaidLeaderboardRow(e.CharacterName, e.TotalDamage))
            .ToListAsync(ct);
    }

    private async Task<int> ComputeDamageAsync(CharacterEntity character, CancellationToken ct)
    {
        var activeMonsters = await db.Monsters.Where(m => m.OwnerCharacterId == character.Id && m.IsInActiveTeam).ToListAsync(ct);

        var damage = 5 + character.Level;
        foreach (var monster in activeMonsters)
        {
            var species = await db.MonsterSpecies.FirstOrDefaultAsync(s => s.Id == monster.SpeciesId, ct);
            damage += MonsterStatMath.ScaledStat(species?.BaseAttack ?? 5, monster.Level, monster.Variant);
        }

        return Math.Max(1, damage);
    }

    private async Task<(CharacterEntity Character, GuildEntity Guild)> ResolveGuildMemberAsync(string sessionToken, Guid characterId, CancellationToken ct)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");

        var membership = await db.GuildMembers.FirstOrDefaultAsync(m => m.CharacterId == characterId, ct)
            ?? throw new AccountOperationException("Vous n'appartenez à aucune guilde.");

        var guild = await db.Guilds.FirstOrDefaultAsync(g => g.Id == membership.GuildId, ct)
            ?? throw new AccountOperationException("Guilde introuvable.");

        return (character, guild);
    }

    private static GuildRaidStatus ToStatus(GuildRaidEntity raid) => new(
        raid.Id, raid.Name, raid.CurrentHealth, raid.MaxHealth, raid.IsAlive, raid.SpawnedAtUtc, raid.KilledAtUtc, raid.KillerCharacterName, raid.BossElement);
}
