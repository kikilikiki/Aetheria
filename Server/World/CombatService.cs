using Aetheria.Database.Context;
using Aetheria.Server.Persistence;
using Aetheria.Server.World.Combat;
using Aetheria.Shared.Models;
using Aetheria.Shared.Models.Combat;
using Microsoft.EntityFrameworkCore;
using CombatActionType = Aetheria.Shared.Enums.CombatActionType;

namespace Aetheria.Server.World;

/// <summary>
/// Combat tactique sur grille (voir <c>Docs/GameDesign.md</c> — section Combats). Mode Solo
/// uniquement pour cette première version : le joueur et jusqu'à 4 de ses créatures contre un
/// monstre sauvage contrôlé par une IA simple. Le mode Coopération (4 joueurs + 1 créature
/// chacun) et les compétences/sorts (portée/zone au-delà de l'attaque de base) restent à faire.
/// </summary>
public sealed class CombatService(AetheriaDbContext db, SessionTokenStore tokenStore, CombatSessionStore combatStore)
{
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

        var playerMonsters = request.MonsterIds.Count == 0
            ? []
            : await db.Monsters
                .Where(m => request.MonsterIds.Contains(m.Id) && m.OwnerCharacterId == character.Id)
                .ToListAsync(ct);

        var combatants = new List<Combatant>
        {
            new()
            {
                Id = character.Id, Name = character.Name, Team = 0, X = 0, Y = 3,
                MaxHealth = 50, CurrentHealth = 50, Attack = 10, Defense = 8, Speed = 10,
                MovementRange = 3, AttackRange = 1, IsPlayerControlled = true,
            },
        };

        (int X, int Y)[] monsterSlots = [(1, 1), (1, 2), (1, 4), (1, 5)];
        for (var i = 0; i < playerMonsters.Count && i < monsterSlots.Length; i++)
        {
            var monster = playerMonsters[i];
            var species = await db.MonsterSpecies.FirstOrDefaultAsync(s => s.Id == monster.SpeciesId, ct);
            var displayName = monster.Nickname.Length > 0 ? monster.Nickname : species?.Name ?? "Créature";

            combatants.Add(new Combatant
            {
                Id = monster.Id, Name = displayName, Team = 0, X = monsterSlots[i].X, Y = monsterSlots[i].Y,
                MaxHealth = Math.Max(1, species?.BaseHealth ?? 20), CurrentHealth = Math.Max(1, species?.BaseHealth ?? 20),
                Attack = species?.BaseAttack ?? 5, Defense = species?.BaseDefense ?? 5, Speed = species?.BaseSpeed ?? 5,
                MovementRange = 3, AttackRange = 1, IsPlayerControlled = true,
            });
        }

        combatants.Add(new Combatant
        {
            Id = Guid.NewGuid(), Name = wildSpecies.Name, Team = 1, X = CombatSession.GridWidth - 1, Y = 3,
            MaxHealth = Math.Max(1, wildSpecies.BaseHealth), CurrentHealth = Math.Max(1, wildSpecies.BaseHealth),
            Attack = wildSpecies.BaseAttack, Defense = wildSpecies.BaseDefense, Speed = wildSpecies.BaseSpeed,
            MovementRange = 2, AttackRange = 1, IsPlayerControlled = false,
        });

        var session = new CombatSession
        {
            Id = Guid.NewGuid(),
            OwnerUserId = userId,
            CharacterId = character.Id,
            Combatants = combatants,
        };

        CombatEngine.Initialize(session);
        CombatEngine.RunAiTurnsUntilPlayerTurn(session);
        combatStore.Add(session);

        return ToState(session);
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

        if (session.OwnerUserId != userId)
        {
            throw new AccountOperationException("Ce combat ne vous appartient pas.");
        }

        if (session.IsFinished)
        {
            throw new AccountOperationException("Ce combat est déjà terminé.");
        }

        var actor = session.CurrentCombatant;
        if (actor is not { IsPlayerControlled: true })
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
                await ResolveCaptureAsync(session, request, ct);
                break;

            default:
                throw new AccountOperationException("Action de combat inconnue.");
        }

        if (!session.IsFinished)
        {
            CombatEngine.RunAiTurnsUntilPlayerTurn(session);
        }

        if (session.IsFinished)
        {
            combatStore.Remove(session.Id);
        }

        return ToState(session);
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

    private async Task ResolveCaptureAsync(CombatSession session, CombatActionRequest request, CancellationToken ct)
    {
        if (request.CaptureItemId is not { } captureItemId)
        {
            throw new AccountOperationException("Objet de capture requis.");
        }

        var wildCombatant = session.Combatants.FirstOrDefault(c => c.Team == 1 && c.IsAlive)
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
            CharacterId = session.CharacterId,
            SpeciesId = species.Id,
            TargetHealthPercent = healthPercent,
            CaptureItemId = captureItemId,
        }, ct);

        session.LastMessage = result.Message;
        session.IsFinished = true;
        session.WinningTeam = 0;
    }

    private static CombatSessionState ToState(CombatSession session) => new(
        session.Id,
        CombatSession.GridWidth,
        CombatSession.GridHeight,
        session.Combatants
            .Select(c => new CombatantState(c.Id, c.Name, c.Team, c.X, c.Y, c.CurrentHealth, c.MaxHealth, c.IsAlive))
            .ToList(),
        session.IsFinished ? null : session.CurrentCombatant?.Id,
        session.IsFinished,
        session.WinningTeam,
        session.LastMessage);
}
