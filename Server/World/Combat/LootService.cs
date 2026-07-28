using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Server.World;
using Aetheria.Shared.Models.Combat;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World.Combat;

/// <summary>
/// Butin de victoire (voir GDD — "on gagne 4 objets à chaque combat/fin de donjon que les
/// joueurs doivent se départager en cliquant dessus"). Partagé avec le groupe du vainqueur s'il
/// en a un (sinon lui seul est éligible). Résolu dès que chaque personnage éligible a réclamé un
/// objet — pas d'attente supplémentaire, puisque le mode Coopération (plusieurs joueurs humains
/// dans le même combat) n'existe pas encore : en pratique aujourd'hui il n'y a qu'un seul
/// réclamant réel par combat (voir Docs/README.md), mais la logique de répartition/tirage
/// aléatoire est déjà celle qui servira une fois ce mode ajouté.
/// </summary>
public sealed class LootService(AetheriaDbContext db, LootSessionStore lootStore, PartyService partyService)
{
    private const int ItemsPerDrop = 4;

    public async Task<LootSessionState?> CreateFromVictoryAsync(Guid winnerCharacterId, CancellationToken ct = default)
    {
        var catalog = await db.Items.ToListAsync(ct);
        if (catalog.Count == 0)
        {
            return null;
        }

        var random = Random.Shared;
        var items = new List<LootItemEntry>();
        for (var i = 0; i < ItemsPerDrop; i++)
        {
            var picked = catalog[random.Next(catalog.Count)];
            items.Add(new LootItemEntry { Index = i, ItemId = picked.Id, Name = picked.Name });
        }

        var party = await partyService.GetForCharacterAsync(winnerCharacterId, ct);
        var eligibleIds = party?.Members.Select(m => m.CharacterId).ToList() ?? [winnerCharacterId];

        var session = new LootSession { Id = Guid.NewGuid(), Items = items, EligibleCharacterIds = eligibleIds };
        lootStore.Add(session);

        return ToState(session);
    }

    public async Task<LootSessionState> ClaimAsync(Guid lootId, LootClaimRequest request, SessionTokenStore tokenStore, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(request.SessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        if (!lootStore.TryGet(lootId, out var session))
        {
            throw new AccountOperationException("Butin introuvable ou déjà réparti.");
        }

        if (session.IsResolved)
        {
            throw new AccountOperationException("Ce butin a déjà été réparti.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == request.CharacterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");

        if (!session.EligibleCharacterIds.Contains(character.Id))
        {
            throw new AccountOperationException("Ce personnage n'est pas éligible à ce butin.");
        }

        if (request.ItemIndex < 0 || request.ItemIndex >= session.Items.Count)
        {
            throw new AccountOperationException("Objet invalide.");
        }

        session.Claims[character.Id] = request.ItemIndex;

        if (session.Claims.Count >= session.EligibleCharacterIds.Count)
        {
            await ResolveAsync(session, ct);
        }

        return ToState(session);
    }

    public bool TryGetState(Guid lootId, out LootSessionState state)
    {
        if (lootStore.TryGet(lootId, out var session))
        {
            state = ToState(session);
            return true;
        }

        state = null!;
        return false;
    }

    private async Task ResolveAsync(LootSession session, CancellationToken ct)
    {
        var winners = LootRoll.Resolve(session.Claims, Random.Shared);

        foreach (var (itemIndex, winnerCharacterId) in winners)
        {
            await GrantItemAsync(winnerCharacterId, session.Items[itemIndex].ItemId, ct);
        }

        session.Winners = winners;
        session.IsResolved = true;
        lootStore.Remove(session.Id);
    }

    private async Task GrantItemAsync(Guid characterId, int itemId, CancellationToken ct)
    {
        var existing = await db.InventoryItems.FirstOrDefaultAsync(i => i.CharacterId == characterId && i.ItemId == itemId, ct);
        if (existing is not null)
        {
            existing.Quantity++;
        }
        else
        {
            db.InventoryItems.Add(new InventoryItemEntity { Id = Guid.NewGuid(), CharacterId = characterId, ItemId = itemId, Quantity = 1 });
        }

        await db.SaveChangesAsync(ct);
    }

    private static LootSessionState ToState(LootSession session) => new()
    {
        LootId = session.Id,
        Items = session.Items,
        EligibleCharacterIds = session.EligibleCharacterIds,
        ClaimedCharacterIds = session.Claims.Keys.ToList(),
        IsResolved = session.IsResolved,
        Winners = session.Winners,
        ClaimCountsByItemIndex = session.Claims.Values.GroupBy(i => i).ToDictionary(g => g.Key, g => g.Count()),
    };
}
