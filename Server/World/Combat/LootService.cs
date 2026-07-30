using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Server.World;
using Aetheria.Shared.Enums;
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

    /// <summary>
    /// Voir GDD/demande utilisateur — "ajoute des % de drop des items après un combat, plus ou
    /// moins rare en fonction de la rareté" : auparavant un tirage strictement uniforme sur tout
    /// le catalogue (un objet Mythique avait exactement la même chance qu'un objet Commun).
    /// Poids décroissant par palier plutôt qu'un pourcentage fixe par objet — reste correct quel
    /// que soit le nombre d'objets ajoutés à un palier donné (voir GDD/H40 — catalogue étendu).
    /// </summary>
    private static readonly Dictionary<Rarity, int> RarityWeight = new()
    {
        [Rarity.Commun] = 100,
        [Rarity.PeuCommun] = 55,
        [Rarity.Rare] = 30,
        [Rarity.Epique] = 15,
        [Rarity.Legendaire] = 7,
        [Rarity.Mythique] = 3,
        [Rarity.Ancestral] = 1,
        [Rarity.Divin] = 1,
        [Rarity.Admin] = 0,
    };

    public async Task<LootSessionState?> CreateFromVictoryAsync(Guid winnerCharacterId, CancellationToken ct = default)
    {
        // Voir GDD/demande utilisateur — "objets que l'on ne peut pas obtenir ni fabriquer" :
        // exclus du tirage aléatoire (voir ItemEntity.IsObtainable).
        var catalog = await db.Items.Where(i => i.IsObtainable).ToListAsync(ct);
        if (catalog.Count == 0)
        {
            return null;
        }

        var random = Random.Shared;
        var items = new List<LootItemEntry>();
        for (var i = 0; i < ItemsPerDrop; i++)
        {
            var picked = PickWeightedRandom(catalog, random);
            items.Add(new LootItemEntry { Index = i, ItemId = picked.Id, Name = picked.Name, Rarity = picked.Rarity });
        }

        var party = await partyService.GetForCharacterAsync(winnerCharacterId, ct);
        var eligibleIds = party?.Members.Select(m => m.CharacterId).ToList() ?? [winnerCharacterId];

        var session = new LootSession { Id = Guid.NewGuid(), Items = items, EligibleCharacterIds = eligibleIds };
        lootStore.Add(session);

        return ToState(session);
    }

    /// <summary>Tirage pondéré par rareté (voir <see cref="RarityWeight"/>) — retombe sur un tirage uniforme si tout le catalogue avait un poids nul (cas limite improbable, ex. catalogue réduit aux seuls objets Admin).</summary>
    private static ItemEntity PickWeightedRandom(IReadOnlyList<ItemEntity> catalog, Random random)
    {
        var totalWeight = catalog.Sum(i => RarityWeight.GetValueOrDefault(i.Rarity, 1));
        if (totalWeight <= 0)
        {
            return catalog[random.Next(catalog.Count)];
        }

        var roll = random.Next(totalWeight);
        var cumulative = 0;
        foreach (var item in catalog)
        {
            cumulative += RarityWeight.GetValueOrDefault(item.Rarity, 1);
            if (roll < cumulative)
            {
                return item;
            }
        }

        return catalog[^1];
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

        // Ne retire plus la session immédiatement (voir LootSessionStore — purgée après un court
        // délai) : un coéquipier dont le client sonde encore l'état après la résolution (voir
        // GDD/demande utilisateur — "la première personne à avoir fait le choix a connexion au
        // serveur impossible") doit pouvoir le lire comme résolu, pas comme introuvable (404).
        session.ResolvedAtUtc = DateTime.UtcNow;
    }

    // Voir GDD/demande utilisateur — "limite de stack d'item à 99 par item dans l'inventaire".
    private async Task GrantItemAsync(Guid characterId, int itemId, CancellationToken ct)
    {
        var maxStackSize = await db.Items.Where(i => i.Id == itemId).Select(i => i.MaxStackSize).FirstOrDefaultAsync(ct);

        // Voir GDD/demande utilisateur — "commandes admin abuse : double butin" (voir GlobalEventService).
        await InventoryStackingService.AddQuantityAsync(db, characterId, itemId, GlobalEventService.LootMultiplier, maxStackSize <= 0 ? 99 : maxStackSize, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Résout automatiquement tout butin non entièrement réclamé depuis plus de
    /// <paramref name="timeout"/> (voir GDD/demande utilisateur — "timer de 10 secondes pour le
    /// choix des gains") : les joueurs n'ayant pas encore choisi ne remportent simplement aucun
    /// objet (voir <see cref="LootRoll.Resolve"/>, déjà tolérant à des réclamations partielles).
    /// Appelé par <c>CombatTimeoutScheduler</c>, pas par un client.
    /// </summary>
    public async Task ResolveTimedOutAsync(IEnumerable<LootSession> sessions, TimeSpan timeout, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - timeout;
        foreach (var session in sessions)
        {
            if (!session.IsResolved && session.CreatedAtUtc < cutoff)
            {
                await ResolveAsync(session, ct);
            }
        }
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
        CreatedAtUtc = session.CreatedAtUtc,
    };
}
