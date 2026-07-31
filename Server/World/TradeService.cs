using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Voir GDD/demande utilisateur — "Système d'échange (trade) entre joueurs". Contrairement à
/// l'Hôtel des ventes (voir AuctionService, marché anonyme avec enchères), une offre d'échange
/// cible un joueur précis : une créature (optionnelle) plus de l'or contre de l'or demandé.
/// **Simplification assumée** : la contrepartie du joueur ciblé est toujours en or plutôt qu'une
/// de ses propres créatures — évite d'avoir à lui faire parcourir l'équipe de l'initiateur pour
/// composer l'offre, tout en couvrant l'essentiel du besoin (revente/don ciblé entre deux joueurs
/// précis, sans passer par le marché public).
/// </summary>
public sealed class TradeService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    public async Task<string> ProposeAsync(ProposeTradeRequest request, CancellationToken ct = default)
    {
        var character = await ResolveOwnCharacterAsync(request.SessionToken, request.InitiatorCharacterId, ct);
        var target = await db.Characters.FirstOrDefaultAsync(c => c.Name == request.TargetCharacterName, ct)
            ?? throw new AccountOperationException("Joueur introuvable.");

        if (target.Id == character.Id)
        {
            throw new AccountOperationException("Impossible d'échanger avec soi-même.");
        }

        if (request.OfferedGold < 0 || request.RequestedGold < 0)
        {
            throw new AccountOperationException("Les montants d'or ne peuvent pas être négatifs.");
        }

        if (character.Gold < request.OfferedGold)
        {
            throw new AccountOperationException("Vous n'avez pas assez d'or pour cette offre.");
        }

        MonsterEntity? offeredMonster = null;
        if (request.OfferedMonsterId is { } monsterId)
        {
            offeredMonster = await db.Monsters.FirstOrDefaultAsync(m => m.Id == monsterId && m.OwnerCharacterId == character.Id, ct)
                ?? throw new AccountOperationException("Cette créature ne vous appartient pas.");
        }

        db.TradeOffers.Add(new TradeOfferEntity
        {
            Id = Guid.NewGuid(),
            InitiatorCharacterId = character.Id,
            TargetCharacterId = target.Id,
            OfferedMonsterId = offeredMonster?.Id,
            OfferedGold = request.OfferedGold,
            RequestedGold = request.RequestedGold,
        });
        await db.SaveChangesAsync(ct);

        return $"Offre d'échange envoyée à {target.Name}.";
    }

    public async Task<string> RespondAsync(Guid offerId, RespondTradeRequest request, CancellationToken ct = default)
    {
        var character = await ResolveOwnCharacterAsync(request.SessionToken, request.CharacterId, ct);
        var offer = await db.TradeOffers.FirstOrDefaultAsync(o => o.Id == offerId && o.Status == TradeOfferStatus.Pending, ct)
            ?? throw new AccountOperationException("Offre introuvable ou déjà traitée.");

        if (offer.TargetCharacterId != character.Id && offer.InitiatorCharacterId != character.Id)
        {
            throw new AccountOperationException("Cette offre ne vous concerne pas.");
        }

        if (!request.Accept)
        {
            // Voir GDD/demande utilisateur — l'initiateur peut annuler sa propre offre, le
            // destinataire peut la refuser : même effet (rien n'a encore été échangé), statut
            // différent uniquement pour l'affichage côté Client (voir TradeOfferSummary).
            offer.Status = offer.TargetCharacterId == character.Id ? TradeOfferStatus.Declined : TradeOfferStatus.Cancelled;
            await db.SaveChangesAsync(ct);
            return "Offre d'échange refusée.";
        }

        if (offer.TargetCharacterId != character.Id)
        {
            throw new AccountOperationException("Seul le destinataire peut accepter cette offre.");
        }

        var initiator = await db.Characters.FirstOrDefaultAsync(c => c.Id == offer.InitiatorCharacterId, ct)
            ?? throw new AccountOperationException("L'initiateur de l'offre n'existe plus.");

        // Voir GDD/demande utilisateur — revalidation au moment de l'acceptation : l'initiateur a
        // pu dépenser son or ou perdre la créature entre la proposition et l'acceptation.
        if (initiator.Gold < offer.OfferedGold)
        {
            throw new AccountOperationException("L'initiateur n'a plus assez d'or pour cette offre.");
        }

        if (character.Gold < offer.RequestedGold)
        {
            throw new AccountOperationException("Vous n'avez pas assez d'or pour accepter cette offre.");
        }

        MonsterEntity? offeredMonster = null;
        if (offer.OfferedMonsterId is { } monsterId)
        {
            offeredMonster = await db.Monsters.FirstOrDefaultAsync(m => m.Id == monsterId && m.OwnerCharacterId == initiator.Id, ct)
                ?? throw new AccountOperationException("L'initiateur ne possède plus cette créature.");
        }

        initiator.Gold = initiator.Gold - offer.OfferedGold + offer.RequestedGold;
        character.Gold = character.Gold - offer.RequestedGold + offer.OfferedGold;
        if (offeredMonster is not null)
        {
            offeredMonster.OwnerCharacterId = character.Id;
            offeredMonster.IsInActiveTeam = false;
        }

        offer.Status = TradeOfferStatus.Accepted;
        await db.SaveChangesAsync(ct);

        return offeredMonster is not null
            ? $"Échange conclu : {offeredMonster.Nickname} rejoint votre équipe."
            : "Échange conclu.";
    }

    public async Task<IReadOnlyList<TradeOfferSummary>> GetIncomingAsync(Guid characterId, CancellationToken ct = default)
    {
        var offers = await db.TradeOffers
            .Where(o => o.TargetCharacterId == characterId && o.Status == TradeOfferStatus.Pending)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(ct);
        return await ToSummariesAsync(offers, ct);
    }

    public async Task<IReadOnlyList<TradeOfferSummary>> GetOutgoingAsync(Guid characterId, CancellationToken ct = default)
    {
        var offers = await db.TradeOffers
            .Where(o => o.InitiatorCharacterId == characterId && o.Status == TradeOfferStatus.Pending)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(ct);
        return await ToSummariesAsync(offers, ct);
    }

    private async Task<IReadOnlyList<TradeOfferSummary>> ToSummariesAsync(List<TradeOfferEntity> offers, CancellationToken ct)
    {
        if (offers.Count == 0)
        {
            return [];
        }

        var characterIds = offers.Select(o => o.InitiatorCharacterId).Concat(offers.Select(o => o.TargetCharacterId)).Distinct().ToList();
        var names = await db.Characters.Where(c => characterIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var monsterIds = offers.Where(o => o.OfferedMonsterId is not null).Select(o => o.OfferedMonsterId!.Value).ToList();
        var monsterNames = await db.Monsters.Where(m => monsterIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Nickname, ct);

        return offers.Select(o => new TradeOfferSummary
        {
            Id = o.Id,
            InitiatorName = names.GetValueOrDefault(o.InitiatorCharacterId, "?"),
            TargetName = names.GetValueOrDefault(o.TargetCharacterId, "?"),
            OfferedMonsterName = o.OfferedMonsterId is { } id ? monsterNames.GetValueOrDefault(id, "?") : null,
            OfferedGold = o.OfferedGold,
            RequestedGold = o.RequestedGold,
            Status = o.Status,
            CreatedAtUtc = o.CreatedAtUtc,
        }).ToList();
    }

    private async Task<CharacterEntity> ResolveOwnCharacterAsync(string sessionToken, Guid characterId, CancellationToken ct)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        return await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");
    }
}
