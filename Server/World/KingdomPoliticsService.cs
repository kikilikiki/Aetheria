using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Voir GDD/demande utilisateur — "Fonctionnalités de royaume avancées" : élections du roi,
/// taxes (avec exemption au palier premium 3) et construction de bâtiments financée par le
/// trésor de guerre ainsi collecté. Réutilise <see cref="KingdomEntity.BonusTerritoryCount"/>
/// (déjà le mécanisme de "bâtiment" du royaume, voir <see cref="KingdomWarService"/>) plutôt que
/// d'introduire un second système de bâtiments distinct.
/// </summary>
public sealed class KingdomPoliticsService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    /// <summary>Voir GDD/demande utilisateur — "taxes".</summary>
    public const int TaxPercent = 5;

    /// <summary>Voir GDD/demande utilisateur — "construction de bâtiments".</summary>
    public const long BuildingCostGold = 5000;

    /// <summary>
    /// Prélève <see cref="TaxPercent"/>% d'un gain d'or au profit du trésor du royaume du
    /// personnage, sauf exemption (Fondateur ou palier premium 3 — voir GDD/demande utilisateur
    /// "premium tier 3 = tax exemption"). Retourne le montant net à créditer au personnage.
    /// </summary>
    public static async Task<long> ApplyTaxAsync(AetheriaDbContext db, CharacterEntity character, long grossGoldGain, CancellationToken ct = default)
    {
        if (grossGoldGain <= 0)
        {
            return grossGoldGain;
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == character.UserId, ct);
        if (user is null || user.Rank == UserRank.Fondateur || user.PremiumGradeTier >= 3)
        {
            return grossGoldGain;
        }

        var tax = grossGoldGain * TaxPercent / 100;
        if (tax <= 0)
        {
            return grossGoldGain;
        }

        var kingdom = await db.Kingdoms.FirstOrDefaultAsync(k => k.Type == character.Kingdom, ct);
        if (kingdom is not null)
        {
            kingdom.TreasuryGold += tax;
        }

        return grossGoldGain - tax;
    }

    /// <summary>Voir GDD/demande utilisateur — "élections du roi" : vote (ou revote) pour un candidat du même royaume.</summary>
    public async Task VoteAsync(VoteForKingRequest request, CancellationToken ct = default)
    {
        var voter = await ResolveOwnedCharacterAsync(request.SessionToken, request.CharacterId, ct);
        var candidate = await db.Characters.FirstOrDefaultAsync(c => c.Name == request.CandidateName, ct)
            ?? throw new AccountOperationException("Candidat introuvable.");

        if (candidate.Kingdom != voter.Kingdom)
        {
            throw new AccountOperationException("Le candidat doit appartenir à votre royaume.");
        }

        var kingdom = await db.Kingdoms.FirstAsync(k => k.Type == voter.Kingdom, ct);
        var existingVote = await db.KingdomVotes.FirstOrDefaultAsync(v => v.KingdomId == kingdom.Id && v.VoterCharacterId == voter.Id, ct);
        if (existingVote is null)
        {
            db.KingdomVotes.Add(new KingdomVoteEntity { Id = Guid.NewGuid(), KingdomId = kingdom.Id, VoterCharacterId = voter.Id, CandidateCharacterId = candidate.Id });
        }
        else
        {
            existingVote.CandidateCharacterId = candidate.Id;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Voir GDD/demande utilisateur — "élections du roi" : dépouille les votes de chaque royaume
    /// et élit le candidat avec le plus de voix (appelé chaque semaine, même cadence que la
    /// guerre de royaumes — voir <c>KingdomWarScheduler</c>).
    /// </summary>
    public async Task<string> ResolveElectionsAsync(CancellationToken ct = default)
    {
        var kingdoms = await db.Kingdoms.ToListAsync(ct);
        var summary = new List<string>();

        foreach (var kingdom in kingdoms)
        {
            var votes = await db.KingdomVotes.Where(v => v.KingdomId == kingdom.Id).ToListAsync(ct);
            if (votes.Count == 0)
            {
                continue;
            }

            var winnerId = votes.GroupBy(v => v.CandidateCharacterId).OrderByDescending(g => g.Count()).First().Key;
            kingdom.KingCharacterId = winnerId;
            db.KingdomVotes.RemoveRange(votes);

            var winnerName = (await db.Characters.FirstOrDefaultAsync(c => c.Id == winnerId, ct))?.Name ?? "?";
            summary.Add($"{kingdom.Name} : {winnerName}");
        }

        await db.SaveChangesAsync(ct);
        return summary.Count == 0 ? "Aucun vote enregistré." : $"Élections de royaume résolues — {string.Join(", ", summary)}.";
    }

    /// <summary>Voir GDD/demande utilisateur — "construction de bâtiments" : réservé au roi élu, dépense le trésor pour agrandir durablement le bonus de rendement du royaume.</summary>
    public async Task<KingdomPoliticsStatus> ConstructBuildingAsync(ConstructKingdomBuildingRequest request, CancellationToken ct = default)
    {
        var character = await ResolveOwnedCharacterAsync(request.SessionToken, request.CharacterId, ct);
        var kingdom = await db.Kingdoms.FirstAsync(k => k.Type == character.Kingdom, ct);

        if (kingdom.KingCharacterId != character.Id)
        {
            throw new AccountOperationException("Seul le roi élu de votre royaume peut faire construire un bâtiment.");
        }

        if (kingdom.TreasuryGold < BuildingCostGold)
        {
            throw new AccountOperationException($"Trésor du royaume insuffisant (coût : {BuildingCostGold} or).");
        }

        kingdom.TreasuryGold -= BuildingCostGold;
        kingdom.BonusTerritoryCount++;
        await db.SaveChangesAsync(ct);

        return await BuildStatusAsync(character, ct);
    }

    public async Task<KingdomPoliticsStatus> GetStatusAsync(Guid characterId, CancellationToken ct = default)
    {
        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId, ct)
            ?? throw new AccountOperationException("Personnage introuvable.");

        return await BuildStatusAsync(character, ct);
    }

    private async Task<KingdomPoliticsStatus> BuildStatusAsync(CharacterEntity character, CancellationToken ct)
    {
        var kingdom = await db.Kingdoms.FirstAsync(k => k.Type == character.Kingdom, ct);
        var kingName = kingdom.KingCharacterId is null
            ? null
            : (await db.Characters.FirstOrDefaultAsync(c => c.Id == kingdom.KingCharacterId, ct))?.Name;

        var user = await db.Users.FirstAsync(u => u.Id == character.UserId, ct);

        return new KingdomPoliticsStatus
        {
            KingdomId = kingdom.Id,
            KingdomName = kingdom.Name,
            KingCharacterId = kingdom.KingCharacterId,
            KingCharacterName = kingName,
            TreasuryGold = kingdom.TreasuryGold,
            BonusTerritoryCount = kingdom.BonusTerritoryCount,
            IsTaxExempt = user.Rank == UserRank.Fondateur || user.PremiumGradeTier >= 3,
        };
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
}
