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
/// Voir GDD/demande utilisateur — "on peut attaquer plusieurs fois le boss monde, limite le a 3 et
/// fait que sa soit un vrai combat" : engager le combat passe désormais par un vrai combat
/// tactique sur grille (<see cref="Server.World.CombatService.StartWorldBossEncounterAsync"/>),
/// jusqu'à <see cref="MaxAttempts"/> tentatives par joueur et par instance de boss — cette classe
/// ne garde que la partie "état partagé" (invocation, dégâts cumulés, récompenses de mise à mort),
/// <see cref="GetActiveBossAndEntryAsync"/>/<see cref="ApplyDamageAsync"/> sont appelés par
/// <c>CombatService</c> au début/à la fin de chaque tentative.
/// </summary>
public sealed class WorldBossService(AetheriaDbContext db)
{
    /// <summary>Voir GDD/demande utilisateur — "limite le a 3".</summary>
    public const int MaxAttempts = 3;

    /// <summary>Voir GDD/demande utilisateur — "retire le champ espece et royaume pour le boss monde" : l'espèce est tirée au sort dans le catalogue plutôt que choisie par l'admin, et aucun royaume n'est ciblé.</summary>
    public async Task<WorldBossEntity> SpawnAsync(int maxHealth, CancellationToken ct = default)
    {
        var allSpecies = await db.MonsterSpecies.ToListAsync(ct);
        if (allSpecies.Count == 0)
        {
            throw new AccountOperationException("Aucune espèce disponible pour ce boss mondial.");
        }

        var species = allSpecies[Random.Shared.Next(allSpecies.Count)];

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

    /// <summary>
    /// Boss actif + ligne de dégâts du personnage (créée si absente), appelé par
    /// <c>CombatService.StartWorldBossEncounterAsync</c> avant d'engager le combat. Lève si aucun
    /// boss n'est actif ou si les <see cref="MaxAttempts"/> tentatives sont déjà consommées.
    /// </summary>
    public async Task<(WorldBossEntity Boss, WorldBossDamageEntity Entry)> GetActiveBossAndEntryAsync(Guid characterId, string characterName, CancellationToken ct)
    {
        var boss = await db.WorldBosses.Where(b => b.IsAlive).OrderByDescending(b => b.SpawnedAtUtc).FirstOrDefaultAsync(ct)
            ?? throw new AccountOperationException("Aucun boss mondial actif pour le moment.");

        var entry = await db.WorldBossDamageEntries.FirstOrDefaultAsync(e => e.WorldBossId == boss.Id && e.CharacterId == characterId, ct);
        if (entry is null)
        {
            entry = new WorldBossDamageEntity { Id = Guid.NewGuid(), WorldBossId = boss.Id, CharacterId = characterId, CharacterName = characterName };
            db.WorldBossDamageEntries.Add(entry);
            await db.SaveChangesAsync(ct);
        }

        if (entry.AttackCount >= MaxAttempts)
        {
            throw new AccountOperationException($"Vous avez déjà utilisé vos {MaxAttempts} tentatives contre ce boss.");
        }

        return (boss, entry);
    }

    /// <summary>
    /// Applique les dégâts d'UNE tentative terminée (victoire, défaite ou fuite — voir GDD/demande
    /// utilisateur "plus on fait de degat plus on a de point" : compte même sur une tentative
    /// perdue) au total de PV partagé, incrémente le compteur de tentatives, et distribue les
    /// récompenses de mise à mort le cas échéant. Retourne vrai si ce coup a tué le boss.
    /// </summary>
    public async Task<bool> ApplyDamageAsync(WorldBossEntity boss, WorldBossDamageEntity entry, CharacterEntity character, int damage, CancellationToken ct)
    {
        entry.AttackCount++;
        entry.TotalDamage += damage;
        boss.CurrentHealth = Math.Max(0, boss.CurrentHealth - damage);

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
                    var rewardMonster = new MonsterEntity
                    {
                        Id = Guid.NewGuid(),
                        OwnerCharacterId = character.Id,
                        SpeciesId = species.Id,
                        Nickname = species.Name,
                        Level = 1,
                        PassiveTalent = PassiveTalentCatalog.RollRandom(Random.Shared),
                        Nature = MonsterNatureCatalog.RollRandom(Random.Shared),
                    };
                    MonsterIvRoller.RollInto(rewardMonster, Random.Shared);
                    db.Monsters.Add(rewardMonster);
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
        return bossKilled;
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
