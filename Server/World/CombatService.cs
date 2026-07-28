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
    public async Task<CombatSessionState> StartAsync(StartCombatRequest request, CancellationToken ct = default, bool isDungeonCombat = false)
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

        // 4 ennemis plutôt qu'un seul (voir GDD/demande utilisateur — "les ennemis sont 4 et pas
        // 1") : même espèce tirée, en formation sur le côté droit de la grille, chacun avec son
        // propre total de PV (pas de mise à l'échelle des stats — un combat 4 contre jusqu'à 4
        // reste équilibré par le nombre plutôt que par des ennemis individuellement plus forts).
        (int X, int Y)[] enemyPositions = [(CombatSession.GridWidth - 1, 1), (CombatSession.GridWidth - 1, 3), (CombatSession.GridWidth - 1, 5), (CombatSession.GridWidth - 2, 3)];
        for (var i = 0; i < enemyPositions.Length; i++)
        {
            var (x, y) = enemyPositions[i];
            combatants.Add(new Combatant
            {
                Id = Guid.NewGuid(), Name = $"{wildSpecies.Name} {i + 1}", Team = 1, X = x, Y = y,
                MaxHealth = Math.Max(1, wildSpecies.BaseHealth), CurrentHealth = Math.Max(1, wildSpecies.BaseHealth),
                Attack = wildSpecies.BaseAttack, Defense = wildSpecies.BaseDefense, Speed = wildSpecies.BaseSpeed,
                MovementRange = 2, AttackRange = 1, IsPlayerControlled = false,
                Type = wildSpecies.Type, Element = wildSpecies.Element,
            });
        }

        var session = new CombatSession { Id = Guid.NewGuid(), IsPvp = false, Combatants = combatants, IsDungeonCombat = isDungeonCombat };
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
        var species = await ResolveDungeonEncounterSpeciesAsync(dungeonId, floorNumber, roomIndex, ct);

        return await StartAsync(new StartCombatRequest
        {
            SessionToken = request.SessionToken,
            CharacterId = request.CharacterId,
            MonsterIds = request.MonsterIds,
            WildSpeciesId = species.Id,
        }, ct, isDungeonCombat: true);
    }

    /// <summary>
    /// Aperçu de la créature qui sera affrontée dans une salle, sans démarrer de combat (voir
    /// GDD/demande utilisateur — "faire en sorte que l'on voie les ennemis avant de les
    /// combattre, comme Pokémon Épée") : même tirage exact que <see cref="StartFromDungeonAsync"/>
    /// (graine stable identique, voir <see cref="DungeonFloorGenerator.StableSeed"/>), donc ce
    /// qui est prévisualisé est toujours ce qui sera réellement affronté.
    /// </summary>
    public async Task<MonsterSpeciesData> GetDungeonEncounterPreviewAsync(int dungeonId, int floorNumber, int roomIndex, CancellationToken ct = default)
    {
        var species = await ResolveDungeonEncounterSpeciesAsync(dungeonId, floorNumber, roomIndex, ct);
        return new MonsterSpeciesData
        {
            Id = species.Id, Name = species.Name, Element = species.Element, Type = species.Type,
            BaseRarity = species.BaseRarity, Habitat = species.Habitat, Lore = species.Lore,
            BaseStats = species.BaseStats, EvolvesIntoSpeciesId = species.EvolvesIntoSpeciesId, EvolutionLevel = species.EvolutionLevel,
        };
    }

    private async Task<MonsterSpeciesEntity> ResolveDungeonEncounterSpeciesAsync(int dungeonId, int floorNumber, int roomIndex, CancellationToken ct)
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
        return candidates[random.Next(candidates.Count)];
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

        // OwnerUserId (par combattant) plutôt que TeamOwnerUserId (par équipe) : plusieurs
        // joueurs humains peuvent partager la même équipe en arène (2v2/3v3/4v4, voir GDD), donc
        // seule l'identité du combattant précis dont c'est le tour fait foi.
        var actor = session.CurrentCombatant;
        if (actor is not { IsPlayerControlled: true } || actor.OwnerUserId != userId)
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

            case CombatActionType.SpecialAbility:
                CombatEngine.ResolveSpecialAbility(session, actor, request.TargetX, request.TargetY);
                if (!session.IsFinished)
                {
                    CombatEngine.AdvanceTurn(session);
                }

                break;

            case CombatActionType.Flee:
                if (session.IsDungeonCombat)
                {
                    throw new AccountOperationException("Impossible de fuir un combat de donjon.");
                }

                session.LastMessage = $"{actor.Name} prend la fuite.";
                session.IsFinished = true;
                session.WinningTeam = null;
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
            if (session.IsArenaMatch)
            {
                await ApplyArenaResultAsync(session, ct);
            }
            else if (session.IsPvp)
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

        // Les créatures alliées (tout combattant de l'équipe du joueur qui n'est pas le
        // personnage lui-même) gagnent aussi de l'XP — n'existait pas du tout avant (voir GDD,
        // "UI pour les montres : monter de niveau").
        var allyMonsterIds = session.Combatants
            .Where(c => c.Team == playerTeam && c.Id != winnerCharacterId)
            .Select(c => c.Id)
            .ToList();

        if (allyMonsterIds.Count > 0)
        {
            var allyMonsters = await db.Monsters.Where(m => allyMonsterIds.Contains(m.Id)).ToListAsync(ct);
            foreach (var monster in allyMonsters)
            {
                MonsterProgressionService.GrantExperience(monster, (int)PveVictoryExperience);
            }

            if (allyMonsters.Count > 0)
            {
                await db.SaveChangesAsync(ct);
            }
        }

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
                OwnerUserId = character.UserId, OwnerCharacterId = character.Id,
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
                OwnerUserId = character.UserId, OwnerCharacterId = character.Id,
                Type = species?.Type ?? MonsterType.Guerrier, Element = species?.Element ?? Element.Neutre,
            });
        }

        return combatants;
    }

    /// <summary>
    /// Démarre un match d'arène classée (voir GDD, <c>ArenaQueueService</c>) : les tickets déjà
    /// appairés (première moitié = équipe 0, seconde moitié = équipe 1) deviennent des
    /// combattants placés via <see cref="BuildTeamCellQueue"/> (une file de cases libres par
    /// équipe, remplie au fur et à mesure des joueurs plutôt qu'un calcul de ligne par format —
    /// robuste même pour le 4v4, le format le plus dense).
    /// </summary>
    public async Task<Guid> StartArenaMatchAsync(ArenaFormat format, IReadOnlyList<ArenaTicket> tickets, CancellationToken ct = default)
    {
        var playersPerTeam = ArenaFormatRules.PlayersPerTeam(format);
        var combatants = new List<Combatant>();
        var session = new CombatSession { Id = Guid.NewGuid(), IsPvp = true, IsArenaMatch = true, Combatants = combatants };
        var teamCellQueues = new Dictionary<int, Queue<(int X, int Y)>>();

        for (var i = 0; i < tickets.Count; i++)
        {
            var team = i / playersPerTeam;
            var indexInTeam = i % playersPerTeam;
            var ticket = tickets[i];

            var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == ticket.CharacterId, ct);
            if (character is null)
            {
                continue;
            }

            if (!session.TeamOwnerUserId.ContainsKey(team))
            {
                session.TeamOwnerUserId[team] = ticket.UserId;
                session.TeamCharacterId[team] = ticket.CharacterId;
            }

            if (!teamCellQueues.TryGetValue(team, out var cellQueue))
            {
                cellQueue = BuildTeamCellQueue(leftSide: team == 0);
                teamCellQueues[team] = cellQueue;
            }

            var maxMonsters = ArenaFormatRules.UnitsForPlayerIndex(format, indexInTeam);
            combatants.AddRange(await BuildArenaTeamCombatantsAsync(character, ticket.MonsterIds, team, cellQueue, maxMonsters, ct));
        }

        CombatEngine.Initialize(session);
        combatStore.Add(session);
        return session.Id;
    }

    /// <summary>File de cases libres pour une équipe (2 colonnes proches du bord × toute la hauteur de la grille = 14 cases), remplie joueur par joueur plutôt qu'un calcul de ligne par format — reste correct même pour le 4v4 (8 combattants max par équipe).</summary>
    private static Queue<(int X, int Y)> BuildTeamCellQueue(bool leftSide)
    {
        int[] xs = leftSide ? [0, 1] : [CombatSession.GridWidth - 1, CombatSession.GridWidth - 2];
        var cells = new Queue<(int, int)>();
        for (var y = 0; y < CombatSession.GridHeight; y++)
        {
            foreach (var x in xs)
            {
                cells.Enqueue((x, y));
            }
        }

        return cells;
    }

    private async Task<List<Combatant>> BuildArenaTeamCombatantsAsync(
        CharacterEntity character, IReadOnlyList<Guid> monsterIds, int team, Queue<(int X, int Y)> cellQueue, int maxMonsters, CancellationToken ct)
    {
        var combatants = new List<Combatant>();
        if (cellQueue.Count == 0)
        {
            return combatants;
        }

        var (charX, charY) = cellQueue.Dequeue();
        combatants.Add(new Combatant
        {
            Id = character.Id, Name = character.Name, Team = team, X = charX, Y = charY,
            MaxHealth = 50, CurrentHealth = 50, Attack = 10, Defense = 8, Speed = 10,
            MovementRange = 3, AttackRange = 1, IsPlayerControlled = true,
            OwnerUserId = character.UserId, OwnerCharacterId = character.Id,
        });

        var playerMonsters = monsterIds.Count == 0
            ? []
            : await db.Monsters
                .Where(m => monsterIds.Contains(m.Id) && m.OwnerCharacterId == character.Id)
                .Take(maxMonsters)
                .ToListAsync(ct);

        foreach (var monster in playerMonsters)
        {
            if (cellQueue.Count == 0)
            {
                break;
            }

            var (mx, my) = cellQueue.Dequeue();
            var species = await db.MonsterSpecies.FirstOrDefaultAsync(s => s.Id == monster.SpeciesId, ct);
            var displayName = monster.Nickname.Length > 0 ? monster.Nickname : species?.Name ?? "Créature";

            combatants.Add(new Combatant
            {
                Id = monster.Id, Name = displayName, Team = team, X = mx, Y = my,
                MaxHealth = Math.Max(1, species?.BaseHealth ?? 20), CurrentHealth = Math.Max(1, species?.BaseHealth ?? 20),
                Attack = species?.BaseAttack ?? 5, Defense = species?.BaseDefense ?? 5, Speed = species?.BaseSpeed ?? 5,
                MovementRange = 3, AttackRange = 1, IsPlayerControlled = true,
                OwnerUserId = character.UserId, OwnerCharacterId = character.Id,
                Type = species?.Type ?? MonsterType.Guerrier, Element = species?.Element ?? Element.Neutre,
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
        var loserStats = await db.Statistics.FirstOrDefaultAsync(s => s.CharacterId == loserCharacterId, ct);

        if (winnerStats is not null && loserStats is not null)
        {
            var winnerRatingBefore = winnerStats.Pvp.CurrentRank;
            var loserRatingBefore = loserStats.Pvp.CurrentRank;

            winnerStats.Pvp.Wins++;
            winnerStats.Pvp.WinStreak++;
            winnerStats.Pvp.CurrentRank = Math.Max(0, ComputeNewElo(winnerRatingBefore, loserRatingBefore, won: true));
            winnerStats.Pvp.BestRank = Math.Max(winnerStats.Pvp.BestRank, winnerStats.Pvp.CurrentRank);

            loserStats.Pvp.Losses++;
            loserStats.Pvp.WinStreak = 0;
            loserStats.Pvp.CurrentRank = Math.Max(0, ComputeNewElo(loserRatingBefore, winnerRatingBefore, won: false));
        }

        await db.SaveChangesAsync(ct);

        var winnerCharacter = await db.Characters.FirstOrDefaultAsync(c => c.Id == winnerCharacterId, ct);
        if (winnerCharacter is not null)
        {
            await new KingdomWarService(db).AwardWarPointsAsync(winnerCharacter.Kingdom, 10, ct);
        }
    }

    /// <summary>
    /// Résultat d'un match d'arène classée (voir GDD — équipes de plusieurs joueurs). Généralise
    /// <see cref="ApplyPvpResultAsync"/> : chaque participant (pas seulement un "représentant"
    /// par équipe) gagne/perd de l'ELO, calculé contre la note moyenne de l'équipe adverse
    /// (méthode standard pour l'ELO par équipe).
    /// </summary>
    private async Task ApplyArenaResultAsync(CombatSession session, CancellationToken ct)
    {
        if (session.WinningTeam is not { } winningTeam)
        {
            return;
        }

        var participants = session.Combatants
            .Where(c => c.OwnerUserId is not null && c.OwnerCharacterId is not null)
            .Select(c => (c.Team, CharacterId: c.OwnerCharacterId!.Value))
            .Distinct()
            .ToList();

        var statsByCharacter = new Dictionary<Guid, StatisticsEntity>();
        foreach (var (_, characterId) in participants)
        {
            var stats = await db.Statistics.FirstOrDefaultAsync(s => s.CharacterId == characterId, ct);
            if (stats is not null)
            {
                statsByCharacter[characterId] = stats;
            }
        }

        var teamAverageRatingBefore = participants
            .GroupBy(p => p.Team)
            .ToDictionary(g => g.Key, g => g.Average(p => statsByCharacter.TryGetValue(p.CharacterId, out var s) ? s.Pvp.CurrentRank : 1000));

        foreach (var (team, characterId) in participants)
        {
            if (!statsByCharacter.TryGetValue(characterId, out var stats))
            {
                continue;
            }

            var won = team == winningTeam;
            var opponentTeam = team == 0 ? 1 : 0;
            var opponentAverageRating = teamAverageRatingBefore.GetValueOrDefault(opponentTeam, 1000);

            stats.Pvp.CurrentRank = Math.Max(0, ComputeNewElo(stats.Pvp.CurrentRank, opponentAverageRating, won));
            stats.Pvp.BestRank = Math.Max(stats.Pvp.BestRank, stats.Pvp.CurrentRank);

            if (won)
            {
                stats.Pvp.Wins++;
                stats.Pvp.WinStreak++;
            }
            else
            {
                stats.Pvp.Losses++;
                stats.Pvp.WinStreak = 0;
            }
        }

        await db.SaveChangesAsync(ct);

        foreach (var (_, characterId) in participants.Where(p => p.Team == winningTeam))
        {
            var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId, ct);
            if (character is not null)
            {
                await new KingdomWarService(db).AwardWarPointsAsync(character.Kingdom, 10, ct);
            }
        }
    }

    /// <summary>
    /// Formule ELO standard (K=32) : plus la victoire est "attendue" (adversaire nettement plus
    /// faible), moins elle rapporte — et une défaite contre un adversaire nettement plus fort
    /// coûte peu. Voir GDD — "ligues ELO".
    /// </summary>
    private static int ComputeNewElo(int rating, double opponentRating, bool won)
    {
        const int k = 32;
        var expectedScore = 1.0 / (1.0 + Math.Pow(10, (opponentRating - rating) / 400.0));
        var actualScore = won ? 1.0 : 0.0;
        return (int)Math.Round(rating + k * (actualScore - expectedScore));
    }

    private static CombatSessionState ToState(CombatSession session, Guid? lootId = null) => new(
        session.Id,
        CombatSession.GridWidth,
        CombatSession.GridHeight,
        session.Combatants
            .Select(c => new CombatantState(c.Id, c.Name, c.Team, c.X, c.Y, c.CurrentHealth, c.MaxHealth, c.IsAlive, c.MovementRange, c.AttackRange, c.Type, c.Element))
            .ToList(),
        session.IsFinished ? null : session.CurrentCombatant?.Id,
        session.IsFinished,
        session.WinningTeam,
        session.LastMessage,
        lootId);
}
