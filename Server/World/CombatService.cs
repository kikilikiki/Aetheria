using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Server.World.Combat;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Aetheria.Shared.Models.Combat;
using Microsoft.EntityFrameworkCore;
using CombatActionType = Aetheria.Shared.Enums.CombatActionType;

namespace Aetheria.Server.World;

/// <summary>
/// Combat tactique sur grille (voir <c>Docs/GameDesign.md</c> — section Combats) : mode PvE
/// (joueur + jusqu'à 4 créatures contre un monstre sauvage, IA simple) et mode PvP (deux
/// joueurs, défi direct). Le mode Coopération (4 joueurs contre un monstre) et les
/// compétences/sorts (au-delà de l'attaque de base) restent à faire.
/// </summary>
public sealed class CombatService(AetheriaDbContext db, SessionTokenStore tokenStore, CombatSessionStore combatStore, LootSessionStore lootStore)
{
    /// <summary>XP de base accordée à la victoire PvE (voir GDD — partagée en groupe via <see cref="PartyService"/>). Simplification assumée : montant fixe plutôt que calculé sur le niveau/rareté exacte de la créature vaincue.</summary>
    private const long PveVictoryExperience = 30;
    public async Task<CombatSessionState> StartAsync(StartCombatRequest request, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(request.SessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == request.CharacterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");

        var wildSpecies = await db.MonsterSpecies.FirstOrDefaultAsync(s => s.Id == request.WildSpeciesId, ct)
            ?? throw new AccountOperationException("Espèce de créature inconnue.");

        var combatants = await BuildPlayerCombatantsAsync(character, request.MonsterIds, team: 0, leftSide: true, ct);

        combatants.Add(new Combatant
        {
            Id = Guid.NewGuid(), Name = wildSpecies.Name, Team = 1, X = CombatSession.GridWidth - 1, Y = 3,
            MaxHealth = Math.Max(1, wildSpecies.BaseHealth), CurrentHealth = Math.Max(1, wildSpecies.BaseHealth),
            Attack = wildSpecies.BaseAttack, Defense = wildSpecies.BaseDefense, Speed = wildSpecies.BaseSpeed,
            MovementRange = 2, AttackRange = 1, IsPlayerControlled = false,
        });

        var session = new CombatSession { Id = Guid.NewGuid(), IsPvp = false, Combatants = combatants };
        session.TeamOwnerUserId[0] = userId;
        session.TeamCharacterId[0] = character.Id;

        CombatEngine.Initialize(session);
        CombatEngine.RunAiTurnsUntilPlayerTurn(session);
        combatStore.Add(session);

        return ToState(session);
    }

    /// <summary>
    /// Rencontre sauvage hors donjon (voir GDD — "les mobs sauvages hors donjon sont scalés sur
    /// le niveau du joueur ou du chef de groupe"). Contrairement à <see cref="StartAsync"/>,
    /// l'espèce n'est pas choisie par le client : le serveur la tire lui-même, avec une rareté
    /// dépendant du niveau de la référence de scaling (le chef de groupe si le personnage est en
    /// groupe, sinon lui-même — voir <see cref="PartyService.ResolveScalingReferenceAsync"/>).
    /// </summary>
    public async Task<CombatSessionState> StartWildEncounterAsync(StartWildEncounterRequest request, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(request.SessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == request.CharacterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");

        var partyService = new PartyService(db, tokenStore);
        var scalingReference = await partyService.ResolveScalingReferenceAsync(character.Id, ct);

        var rarity = RarityForLevel(scalingReference.Level);
        var candidates = await db.MonsterSpecies.Where(s => s.BaseRarity == rarity).ToListAsync(ct);
        if (candidates.Count == 0)
        {
            candidates = await db.MonsterSpecies.Where(s => s.BaseRarity == Rarity.Commun).ToListAsync(ct);
        }

        if (candidates.Count == 0)
        {
            throw new AccountOperationException("Aucune créature sauvage disponible.");
        }

        var species = candidates[Random.Shared.Next(candidates.Count)];

        return await StartAsync(new StartCombatRequest
        {
            SessionToken = request.SessionToken,
            CharacterId = request.CharacterId,
            MonsterIds = request.MonsterIds,
            WildSpeciesId = species.Id,
        }, ct);
    }

    /// <summary>
    /// Simplification assumée : paliers de niveau fixes plutôt qu'une formule continue — voir
    /// <c>Docs/README.md</c>. Le chef de groupe sert de référence, pas la moyenne du groupe.
    /// </summary>
    private static Rarity RarityForLevel(int level) => level switch
    {
        <= 5 => Rarity.Commun,
        <= 15 => Rarity.PeuCommun,
        <= 25 => Rarity.Rare,
        <= 35 => Rarity.Epique,
        <= 50 => Rarity.Legendaire,
        _ => Rarity.Mythique,
    };

    /// <summary>Défi PvP direct entre deux personnages (voir GDD — section PvP). Pas de matchmaking pour cette version.</summary>
    public async Task<CombatSessionState> StartPvpAsync(StartPvpCombatRequest request, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(request.SessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var challenger = await db.Characters.FirstOrDefaultAsync(c => c.Id == request.CharacterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");

        var opponent = await db.Characters.FirstOrDefaultAsync(c => c.Id == request.OpponentCharacterId, ct)
            ?? throw new AccountOperationException("Adversaire introuvable.");

        if (opponent.Id == challenger.Id)
        {
            throw new AccountOperationException("Impossible de vous défier vous-même.");
        }

        var combatants = await BuildPlayerCombatantsAsync(challenger, request.MonsterIds, team: 0, leftSide: true, ct);
        combatants.AddRange(await BuildPlayerCombatantsAsync(opponent, request.OpponentMonsterIds, team: 1, leftSide: false, ct));

        var session = new CombatSession { Id = Guid.NewGuid(), IsPvp = true, Combatants = combatants };
        session.TeamOwnerUserId[0] = userId;
        session.TeamCharacterId[0] = challenger.Id;
        session.TeamOwnerUserId[1] = opponent.UserId;
        session.TeamCharacterId[1] = opponent.Id;

        CombatEngine.Initialize(session);
        combatStore.Add(session);

        return ToState(session);
    }

    /// <summary>
    /// Engage le combat contre le monstre d'une salle de donjon générée procéduralement
    /// (voir <c>DungeonFloorGenerator</c>). La rareté de la créature choisie dépend du type de
    /// rencontre (mini-boss/boss/boss légendaire = créatures plus rares), le tirage précis est
    /// déterministe (mêmes graines que la génération de l'étage).
    /// </summary>
    public async Task<CombatSessionState> StartFromDungeonAsync(
        int dungeonId, int floorNumber, int roomIndex, StartDungeonCombatRequest request, CancellationToken ct = default)
    {
        var dungeon = await db.Dungeons.FirstOrDefaultAsync(d => d.Id == dungeonId, ct)
            ?? throw new AccountOperationException("Donjon introuvable.");

        var floor = DungeonFloorGenerator.GenerateFloor(dungeon.Seed, floorNumber);
        var room = floor.Rooms.FirstOrDefault(r => r.Index == roomIndex)
            ?? throw new AccountOperationException("Salle introuvable à cet étage.");

        Rarity? requiredRarity = room.EncounterType switch
        {
            DungeonEncounterType.Monstre => null,
            DungeonEncounterType.MiniBoss => Rarity.Rare,
            DungeonEncounterType.Boss or DungeonEncounterType.BossLegendaire => Rarity.Legendaire,
            _ => throw new AccountOperationException("Cette salle ne contient pas de monstre à combattre."),
        };

        var candidates = requiredRarity is { } rarity
            ? await db.MonsterSpecies.Where(s => s.BaseRarity == rarity).ToListAsync(ct)
            : await db.MonsterSpecies.Where(s => s.BaseRarity == Rarity.Commun || s.BaseRarity == Rarity.PeuCommun).ToListAsync(ct);

        if (candidates.Count == 0)
        {
            throw new AccountOperationException("Aucune créature disponible pour cette rencontre.");
        }

        var random = new Random(DungeonFloorGenerator.StableSeed(dungeon.Seed, floorNumber, roomIndex));
        var species = candidates[random.Next(candidates.Count)];

        return await StartAsync(new StartCombatRequest
        {
            SessionToken = request.SessionToken,
            CharacterId = request.CharacterId,
            MonsterIds = request.MonsterIds,
            WildSpeciesId = species.Id,
        }, ct);
    }

    public async Task<CombatSessionState> SubmitActionAsync(Guid combatId, CombatActionRequest request, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(request.SessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        if (!combatStore.TryGet(combatId, out var session))
        {
            throw new AccountOperationException("Combat introuvable ou terminé.");
        }

        if (session.IsFinished)
        {
            throw new AccountOperationException("Ce combat est déjà terminé.");
        }

        var actor = session.CurrentCombatant;
        if (actor is not { IsPlayerControlled: true }
            || !session.TeamOwnerUserId.TryGetValue(actor.Team, out var expectedUserId)
            || expectedUserId != userId)
        {
            throw new AccountOperationException("Ce n'est pas votre tour.");
        }

        switch (request.ActionType)
        {
            case CombatActionType.Move:
                CombatEngine.ResolveMove(session, actor, request.TargetX, request.TargetY);
                CombatEngine.AdvanceTurn(session);
                break;

            case CombatActionType.Attack:
                CombatEngine.ResolveAttack(session, actor, request.TargetX, request.TargetY);
                if (!session.IsFinished)
                {
                    CombatEngine.AdvanceTurn(session);
                }

                break;

            case CombatActionType.Pass:
                session.LastMessage = $"{actor.Name} passe son tour.";
                CombatEngine.AdvanceTurn(session);
                break;

            case CombatActionType.Capture:
                if (session.IsPvp)
                {
                    throw new AccountOperationException("Impossible de capturer un autre joueur.");
                }

                await ResolveCaptureAsync(session, actor, request, ct);
                break;

            default:
                throw new AccountOperationException("Action de combat inconnue.");
        }

        if (!session.IsFinished)
        {
            CombatEngine.RunAiTurnsUntilPlayerTurn(session);
        }

        Guid? lootId = null;
        if (session.IsFinished)
        {
            if (session.IsPvp)
            {
                await ApplyPvpResultAsync(session, ct);
            }
            else if (request.ActionType != CombatActionType.Capture)
            {
                lootId = await ApplyPveVictoryRewardsAsync(session, ct);
            }

            combatStore.Remove(session.Id);
        }

        return ToState(session, lootId);
    }

    /// <summary>
    /// XP + butin de victoire PvE (voir GDD). Rien n'est accordé si le joueur a perdu, ni pour
    /// une victoire par capture réussie (le monstre capturé disparaît du combat, la capture est
    /// déjà sa propre récompense — voir <see cref="ResolveCaptureAsync"/>).
    /// </summary>
    private async Task<Guid?> ApplyPveVictoryRewardsAsync(CombatSession session, CancellationToken ct)
    {
        const int playerTeam = 0;
        if (session.WinningTeam != playerTeam || !session.TeamCharacterId.TryGetValue(playerTeam, out var winnerCharacterId))
        {
            return null;
        }

        var partyService = new PartyService(db, tokenStore);
        await partyService.GrantSharedExperienceAsync(winnerCharacterId, PveVictoryExperience, ct);

        var lootService = new LootService(db, lootStore, partyService);
        var loot = await lootService.CreateFromVictoryAsync(winnerCharacterId, ct);
        return loot?.LootId;
    }

    public bool TryGetState(Guid combatId, out CombatSessionState state)
    {
        if (combatStore.TryGet(combatId, out var session))
        {
            state = ToState(session);
            return true;
        }

        state = null!;
        return false;
    }

    private async Task<List<Combatant>> BuildPlayerCombatantsAsync(
        CharacterEntity character, IReadOnlyList<Guid> monsterIds, int team, bool leftSide, CancellationToken ct)
    {
        var characterX = leftSide ? 0 : CombatSession.GridWidth - 1;
        var monsterX = leftSide ? 1 : CombatSession.GridWidth - 2;

        var combatants = new List<Combatant>
        {
            new()
            {
                Id = character.Id, Name = character.Name, Team = team, X = characterX, Y = 3,
                MaxHealth = 50, CurrentHealth = 50, Attack = 10, Defense = 8, Speed = 10,
                MovementRange = 3, AttackRange = 1, IsPlayerControlled = true,
            },
        };

        var playerMonsters = monsterIds.Count == 0
            ? []
            : await db.Monsters
                .Where(m => monsterIds.Contains(m.Id) && m.OwnerCharacterId == character.Id)
                .ToListAsync(ct);

        (int X, int Y)[] monsterSlots = [(monsterX, 1), (monsterX, 2), (monsterX, 4), (monsterX, 5)];
        for (var i = 0; i < playerMonsters.Count && i < monsterSlots.Length; i++)
        {
            var monster = playerMonsters[i];
            var species = await db.MonsterSpecies.FirstOrDefaultAsync(s => s.Id == monster.SpeciesId, ct);
            var displayName = monster.Nickname.Length > 0 ? monster.Nickname : species?.Name ?? "Créature";

            combatants.Add(new Combatant
            {
                Id = monster.Id, Name = displayName, Team = team, X = monsterSlots[i].X, Y = monsterSlots[i].Y,
                MaxHealth = Math.Max(1, species?.BaseHealth ?? 20), CurrentHealth = Math.Max(1, species?.BaseHealth ?? 20),
                Attack = species?.BaseAttack ?? 5, Defense = species?.BaseDefense ?? 5, Speed = species?.BaseSpeed ?? 5,
                MovementRange = 3, AttackRange = 1, IsPlayerControlled = true,
            });
        }

        return combatants;
    }

    private async Task ResolveCaptureAsync(CombatSession session, Combatant actor, CombatActionRequest request, CancellationToken ct)
    {
        if (request.CaptureItemId is not { } captureItemId)
        {
            throw new AccountOperationException("Objet de capture requis.");
        }

        var wildCombatant = session.Combatants.FirstOrDefault(c => c.Team != actor.Team && c.IsAlive)
            ?? throw new AccountOperationException("Aucun monstre sauvage à capturer.");

        // L'identifiant d'espèce n'est pas dupliqué sur le combattant : on le retrouve par nom.
        // Limite assumée pour cette version — un identifiant explicite serait plus robuste
        // dès que deux espèces pourraient partager un nom.
        var species = await db.MonsterSpecies.FirstOrDefaultAsync(s => s.Name == wildCombatant.Name, ct)
            ?? throw new AccountOperationException("Espèce introuvable pour la capture.");

        var healthPercent = (int)Math.Round(100.0 * wildCombatant.CurrentHealth / wildCombatant.MaxHealth);

        var captureService = new CaptureService(db, tokenStore);
        var result = await captureService.AttemptCaptureAsync(new CaptureAttemptRequest
        {
            SessionToken = request.SessionToken,
            CharacterId = session.TeamCharacterId[actor.Team],
            SpeciesId = species.Id,
            TargetHealthPercent = healthPercent,
            CaptureItemId = captureItemId,
        }, ct);

        session.LastMessage = result.Message;
        session.IsFinished = true;
        session.WinningTeam = actor.Team;
    }

    private async Task ApplyPvpResultAsync(CombatSession session, CancellationToken ct)
    {
        if (session.WinningTeam is not { } winningTeam)
        {
            return;
        }

        var losingTeam = winningTeam == 0 ? 1 : 0;
        if (!session.TeamCharacterId.TryGetValue(winningTeam, out var winnerCharacterId)
            || !session.TeamCharacterId.TryGetValue(losingTeam, out var loserCharacterId))
        {
            return;
        }

        var winnerStats = await db.Statistics.FirstOrDefaultAsync(s => s.CharacterId == winnerCharacterId, ct);
        if (winnerStats is not null)
        {
            winnerStats.Pvp.Wins++;
            winnerStats.Pvp.WinStreak++;
            winnerStats.Pvp.CurrentRank += 10;
            winnerStats.Pvp.BestRank = Math.Max(winnerStats.Pvp.BestRank, winnerStats.Pvp.CurrentRank);
        }

        var loserStats = await db.Statistics.FirstOrDefaultAsync(s => s.CharacterId == loserCharacterId, ct);
        if (loserStats is not null)
        {
            loserStats.Pvp.Losses++;
            loserStats.Pvp.WinStreak = 0;
            loserStats.Pvp.CurrentRank = Math.Max(0, loserStats.Pvp.CurrentRank - 5);
        }

        await db.SaveChangesAsync(ct);

        var winnerCharacter = await db.Characters.FirstOrDefaultAsync(c => c.Id == winnerCharacterId, ct);
        if (winnerCharacter is not null)
        {
            await new KingdomWarService(db).AwardWarPointsAsync(winnerCharacter.Kingdom, 10, ct);
        }
    }

    private static CombatSessionState ToState(CombatSession session, Guid? lootId = null) => new(
        session.Id,
        CombatSession.GridWidth,
        CombatSession.GridHeight,
        session.Combatants
            .Select(c => new CombatantState(c.Id, c.Name, c.Team, c.X, c.Y, c.CurrentHealth, c.MaxHealth, c.IsAlive))
            .ToList(),
        session.IsFinished ? null : session.CurrentCombatant?.Id,
        session.IsFinished,
        session.WinningTeam,
        session.LastMessage,
        lootId);
}
