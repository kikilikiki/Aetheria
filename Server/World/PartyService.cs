using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Groupes de joueurs (voir <c>Docs/GameDesign.md</c> — visibilité globale des joueurs même hors
/// groupe, XP partagée en groupe, mobs sauvages hors donjon scalés sur le niveau du chef de
/// groupe). Un personnage n'appartient qu'à un seul groupe à la fois (même contrainte que les
/// guildes — index unique sur <c>PartyMemberEntity.CharacterId</c>).
/// </summary>
public sealed class PartyService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    public const int MaxMembers = 4;

    public async Task<PartySummary> CreateAsync(CreatePartyRequest request, CancellationToken ct = default)
    {
        var character = await ResolveOwnedCharacterAsync(request.SessionToken, request.CharacterId, ct);

        if (await db.PartyMembers.AnyAsync(m => m.CharacterId == character.Id, ct))
        {
            throw new AccountOperationException("Ce personnage appartient déjà à un groupe.");
        }

        var party = new PartyEntity { Id = Guid.NewGuid(), LeaderCharacterId = character.Id };
        db.Parties.Add(party);
        db.PartyMembers.Add(new PartyMemberEntity { Id = Guid.NewGuid(), PartyId = party.Id, CharacterId = character.Id });

        await db.SaveChangesAsync(ct);

        return await BuildSummaryAsync(party.Id, ct);
    }

    public async Task<PartySummary> JoinAsync(Guid partyId, JoinPartyRequest request, CancellationToken ct = default)
    {
        var character = await ResolveOwnedCharacterAsync(request.SessionToken, request.CharacterId, ct);

        if (await db.PartyMembers.AnyAsync(m => m.CharacterId == character.Id, ct))
        {
            throw new AccountOperationException("Ce personnage appartient déjà à un groupe.");
        }

        var party = await db.Parties.FirstOrDefaultAsync(p => p.Id == partyId, ct)
            ?? throw new AccountOperationException("Groupe introuvable.");

        var memberCount = await db.PartyMembers.CountAsync(m => m.PartyId == partyId, ct);
        if (memberCount >= MaxMembers)
        {
            throw new AccountOperationException($"Le groupe est complet ({MaxMembers} joueurs maximum).");
        }

        db.PartyMembers.Add(new PartyMemberEntity { Id = Guid.NewGuid(), PartyId = partyId, CharacterId = character.Id });
        await db.SaveChangesAsync(ct);

        return await BuildSummaryAsync(partyId, ct);
    }

    /// <summary>
    /// Quitte le groupe courant. Si le chef part, le membre restant le plus ancien devient chef ;
    /// si c'était le dernier membre, le groupe est supprimé (pas de groupe vide qui traîne).
    /// </summary>
    public async Task LeaveAsync(LeavePartyRequest request, CancellationToken ct = default)
    {
        var character = await ResolveOwnedCharacterAsync(request.SessionToken, request.CharacterId, ct);

        var membership = await db.PartyMembers.FirstOrDefaultAsync(m => m.CharacterId == character.Id, ct)
            ?? throw new AccountOperationException("Ce personnage n'appartient à aucun groupe.");

        var partyId = membership.PartyId;
        db.PartyMembers.Remove(membership);

        var party = await db.Parties.FirstAsync(p => p.Id == partyId, ct);
        var remaining = await db.PartyMembers
            .Where(m => m.PartyId == partyId && m.Id != membership.Id)
            .OrderBy(m => m.JoinedAtUtc)
            .ToListAsync(ct);

        if (remaining.Count == 0)
        {
            db.Parties.Remove(party);
        }
        else if (party.LeaderCharacterId == character.Id)
        {
            party.LeaderCharacterId = remaining[0].CharacterId;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Groupe du personnage donné, ou <c>null</c> s'il n'en a pas (voir GDD — bouton Groupe en jeu).</summary>
    public async Task<PartySummary?> GetForCharacterAsync(Guid characterId, CancellationToken ct = default)
    {
        var membership = await db.PartyMembers.FirstOrDefaultAsync(m => m.CharacterId == characterId, ct);
        return membership is null ? null : await BuildSummaryAsync(membership.PartyId, ct);
    }

    /// <summary>
    /// Personnage de référence pour scaler la difficulté des mobs sauvages hors donjon (voir
    /// GDD) : le chef de groupe si le personnage est en groupe, sinon le personnage lui-même.
    /// </summary>
    public async Task<CharacterEntity> ResolveScalingReferenceAsync(Guid characterId, CancellationToken ct = default)
    {
        var membership = await db.PartyMembers.FirstOrDefaultAsync(m => m.CharacterId == characterId, ct);
        if (membership is null)
        {
            return await db.Characters.FirstAsync(c => c.Id == characterId, ct);
        }

        var party = await db.Parties.FirstAsync(p => p.Id == membership.PartyId, ct);
        return await db.Characters.FirstAsync(c => c.Id == party.LeaderCharacterId, ct);
    }

    /// <summary>
    /// Octroie de l'XP au personnage. S'il est en groupe, TOUS les membres reçoivent le même
    /// montant plein (partage complet, voir GDD — "l'xp est partagé entre tout les membres du
    /// groupe"), pas une XP divisée en parts plus petites entre eux.
    /// </summary>
    public async Task<IReadOnlyList<CharacterEntity>> GrantSharedExperienceAsync(Guid characterId, long amount, CancellationToken ct = default)
    {
        var membership = await db.PartyMembers.FirstOrDefaultAsync(m => m.CharacterId == characterId, ct);

        List<CharacterEntity> recipients;
        if (membership is null)
        {
            recipients = [await db.Characters.FirstAsync(c => c.Id == characterId, ct)];
        }
        else
        {
            var memberIds = await db.PartyMembers
                .Where(m => m.PartyId == membership.PartyId)
                .Select(m => m.CharacterId)
                .ToListAsync(ct);
            recipients = await db.Characters.Where(c => memberIds.Contains(c.Id)).ToListAsync(ct);
        }

        foreach (var recipient in recipients)
        {
            CharacterProgressionService.GrantExperience(recipient, amount);
        }

        await db.SaveChangesAsync(ct);
        return recipients;
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

    private async Task<PartySummary> BuildSummaryAsync(Guid partyId, CancellationToken ct)
    {
        var party = await db.Parties.FirstAsync(p => p.Id == partyId, ct);
        var members = await db.PartyMembers
            .Where(m => m.PartyId == partyId)
            .Join(db.Characters, m => m.CharacterId, c => c.Id, (m, c) => new PartyMemberSummary
            {
                CharacterId = c.Id,
                Name = c.Name,
                Level = c.Level,
            })
            .ToListAsync(ct);

        return new PartySummary
        {
            Id = party.Id,
            LeaderCharacterId = party.LeaderCharacterId,
            Members = members,
        };
    }
}
