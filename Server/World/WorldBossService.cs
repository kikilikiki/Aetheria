using System.Collections.Concurrent;
using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Aetheria.Shared.Models.WorldBoss;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Boss mondial (voir GDD/demande utilisateur — "un boss monde ou le but est de faire un max de
/// degat, plus on fait de degat plus on a de point, ajoute un leaderboard... du boss actuel et de
/// toujours, il a une barre de vie et peut etre tue"). Un seul boss actif à la fois (voir
/// <see cref="SpawnAsync"/>, réservé aux admins — voir <c>/api/admin/game/spawn-world-boss</c> et
/// GDD "boss geant mondial... que tout le monde doit combattre").
///
/// Volontairement PAS raccroché au moteur de combat tactique sur grille (<c>CombatService</c>) :
/// celui-ci suppose des sessions isolées (un petit groupe contre un adversaire à PV propres), pas
/// un très grand nombre de joueurs frappant en parallèle un même total de PV partagé. Ici, chaque
/// joueur "attaque" directement (bouton + cooldown, comme <c>ProfessionService.GatherAsync</c>)
/// avec des dégâts calculés à partir de son équipe active — plus simple, mais couvre exactement le
/// besoin ("faire un max de dégâts").
/// </summary>
public sealed class WorldBossService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    private static readonly ConcurrentDictionary<Guid, DateTime> LastAttackAtUtc = new();
    private static readonly TimeSpan AttackCooldown = TimeSpan.FromSeconds(8);

    public async Task<WorldBossEntity> SpawnAsync(int speciesId, int maxHealth, KingdomType? targetKingdom = null, CancellationToken ct = default)
    {
        var species = await db.MonsterSpecies.FirstOrDefaultAsync(s => s.Id == speciesId, ct)
            ?? throw new AccountOperationException("Espèce introuvable pour ce boss mondial.");

        var previouslyAlive = await db.WorldBosses.Where(b => b.IsAlive).ToListAsync(ct);
        foreach (var previous in previouslyAlive)
        {
            previous.IsAlive = false;
        }

        var boss = new WorldBossEntity
        {
            Id = Guid.NewGuid(),
            Name = species.Name,
            SpeciesId = species.Id,
            BossElement = species.Element,
            MaxHealth = Math.Max(1, maxHealth),
            CurrentHealth = Math.Max(1, maxHealth),
            TargetKingdom = targetKingdom,
        };

        db.WorldBosses.Add(boss);
        await db.SaveChangesAsync(ct);
        return boss;
    }

    public async Task<WorldBossStatus?> GetStatusAsync(CancellationToken ct = default)
    {
        var boss = await db.WorldBosses.OrderByDescending(b => b.SpawnedAtUtc).FirstOrDefaultAsync(ct);
        return boss is null ? null : ToStatus(boss);
    }

    public async Task<WorldBossAttackResponse> AttackAsync(WorldBossAttackRequest request, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(request.SessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == request.CharacterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");

        var boss = await db.WorldBosses.Where(b => b.IsAlive).OrderByDescending(b => b.SpawnedAtUtc).FirstOrDefaultAsync(ct);
        if (boss is null)
        {
            throw new AccountOperationException("Aucun boss mondial actif pour le moment.");
        }

        if (LastAttackAtUtc.TryGetValue(character.Id, out var last) && DateTime.UtcNow - last < AttackCooldown)
        {
            var remaining = AttackCooldown - (DateTime.UtcNow - last);
            throw new AccountOperationException($"Il faut encore attendre {Math.Ceiling(remaining.TotalSeconds)}s avant d'attaquer à nouveau.");
        }

        LastAttackAtUtc[character.Id] = DateTime.UtcNow;

        var damage = await ComputeDamageAsync(character, ct);
        boss.CurrentHealth = Math.Max(0, boss.CurrentHealth - damage);

        var entry = await db.WorldBossDamageEntries
            .FirstOrDefaultAsync(e => e.WorldBossId == boss.Id && e.CharacterId == character.Id, ct);
        if (entry is null)
        {
            entry = new WorldBossDamageEntity { Id = Guid.NewGuid(), WorldBossId = boss.Id, CharacterId = character.Id, CharacterName = character.Name };
            db.WorldBossDamageEntries.Add(entry);
        }

        entry.TotalDamage += damage;

        var bossKilled = false;
        if (boss.CurrentHealth <= 0 && boss.IsAlive)
        {
            boss.IsAlive = false;
            boss.KilledAtUtc = DateTime.UtcNow;
            boss.KillerCharacterName = character.Name;
            bossKilled = true;
            await new AchievementService(db).UnlockAsync(character.UserId, "terrasseur_de_boss_mondial", ct);

            // Voir GDD/demande utilisateur — "Monstres cosmétiques rares" : seule voie
            // d'obtention, une petite chance pour l'auteur du coup fatal.
            const double CosmeticDropChance = 0.08;
            if (Random.Shared.NextDouble() < CosmeticDropChance)
            {
                var cosmeticSpecies = await db.MonsterSpecies.Where(s => s.IsCosmetic).ToListAsync(ct);
                if (cosmeticSpecies.Count > 0)
                {
                    var species = cosmeticSpecies[Random.Shared.Next(cosmeticSpecies.Count)];
                    db.Monsters.Add(new MonsterEntity
                    {
                        Id = Guid.NewGuid(),
                        OwnerCharacterId = character.Id,
                        SpeciesId = species.Id,
                        Nickname = species.Name,
                        Level = 1,
                        PassiveTalent = PassiveTalentCatalog.RollRandom(Random.Shared),
                    });
                }
            }

            // Voir GDD/demande utilisateur — "plus on fait de degat plus on a de point" : la
            // récompense (or) est proportionnelle aux dégâts infligés par CHAQUE participant à
            // cette instance, pas seulement au coup de grâce.
            var participants = await db.WorldBossDamageEntries.Where(e => e.WorldBossId == boss.Id).ToListAsync(ct);
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

            // Voir GDD/demande utilisateur — "refonte du spawn de boss mondial... la recompense va
            // au royaume qui inflige le plus de degats" : en plus (pas à la place) de la
            // récompense individuelle ci-dessus, un bonus supplémentaire pour tous les
            // participants du royaume ayant cumulé le plus de dégâts au total.
            var damageByKingdom = participants
                .Select(p => (Kingdom: participantCharacters.FirstOrDefault(c => c.Id == p.CharacterId)?.Kingdom, p.TotalDamage))
                .Where(p => p.Kingdom is not null)
                .GroupBy(p => p.Kingdom!.Value)
                .Select(g => (Kingdom: g.Key, TotalDamage: g.Sum(p => p.TotalDamage)))
                .OrderByDescending(g => g.TotalDamage)
                .ToList();

            if (damageByKingdom.Count > 0)
            {
                var winningKingdom = damageByKingdom[0].Kingdom;
                boss.WinningKingdom = winningKingdom;

                const long KingdomBonusGold = 200L;
                foreach (var participantCharacter in participantCharacters.Where(c => c.Kingdom == winningKingdom))
                {
                    participantCharacter.Gold += KingdomBonusGold;
                }
            }
        }

        await db.SaveChangesAsync(ct);

        var message = bossKilled
            ? $"{boss.Name} a été vaincu par {character.Name} ! Récompenses distribuées à tous les participants."
            : $"{damage} dégâts infligés à {boss.Name} ({boss.CurrentHealth}/{boss.MaxHealth} PV restants).";

        return new WorldBossAttackResponse(true, message, damage, entry.TotalDamage, bossKilled, boss.CurrentHealth);
    }

    /// <summary>Somme de l'Attaque effective (mise à l'échelle par niveau + variante, voir MonsterStatMath) des créatures actives, plus une base liée au niveau du personnage.</summary>
    private async Task<int> ComputeDamageAsync(CharacterEntity character, CancellationToken ct)
    {
        var activeMonsters = await db.Monsters
            .Where(m => m.OwnerCharacterId == character.Id && m.IsInActiveTeam)
            .ToListAsync(ct);

        var damage = 5 + character.Level;
        foreach (var monster in activeMonsters)
        {
            var species = await db.MonsterSpecies.FirstOrDefaultAsync(s => s.Id == monster.SpeciesId, ct);
            damage += MonsterStatMath.ScaledStat(species?.BaseAttack ?? 5, monster.Level, monster.Variant);
        }

        return Math.Max(1, damage);
    }

    public async Task<List<WorldBossLeaderboardRow>> GetCurrentLeaderboardAsync(int limit, CancellationToken ct = default)
    {
        var boss = await db.WorldBosses.OrderByDescending(b => b.SpawnedAtUtc).FirstOrDefaultAsync(ct);
        if (boss is null)
        {
            return [];
        }

        return await db.WorldBossDamageEntries
            .Where(e => e.WorldBossId == boss.Id)
            .OrderByDescending(e => e.TotalDamage)
            .Take(limit)
            .Select(e => new WorldBossLeaderboardRow(e.CharacterName, e.TotalDamage))
            .ToListAsync(ct);
    }

    /// <summary>Voir GDD/demande utilisateur — "de toujours" : somme des dégâts d'un personnage sur TOUTES les instances de boss mondial, pas seulement l'actuelle.</summary>
    public async Task<List<WorldBossLeaderboardRow>> GetAllTimeLeaderboardAsync(int limit, CancellationToken ct = default)
    {
        return await db.WorldBossDamageEntries
            .GroupBy(e => e.CharacterName)
            .Select(g => new WorldBossLeaderboardRow(g.Key, g.Sum(e => e.TotalDamage)))
            .OrderByDescending(r => r.TotalDamage)
            .Take(limit)
            .ToListAsync(ct);
    }

    private static WorldBossStatus ToStatus(WorldBossEntity boss) => new(
        boss.Id, boss.Name, boss.CurrentHealth, boss.MaxHealth, boss.IsAlive, boss.SpawnedAtUtc, boss.KilledAtUtc, boss.KillerCharacterName, boss.TargetKingdom,
        boss.BossElement, boss.WinningKingdom);
}
