using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Donner un objet à une créature (voir GDD — UI de gestion des montres : "monter de niveau,
/// objet à donner"). Voir Docs/Idees.md — quelques objets nommés (voir
/// <see cref="GiveItemEffectByName"/>) ont désormais un effet propre (bonus d'EV permanent)
/// plutôt que l'XP fixe accordée par défaut à tout le reste — même principe de reconnaissance
/// par <c>Item.Name</c> que <see cref="RerollPassiveTalentAsync"/>/<see cref="RerollIvAsync"/>
/// ci-dessous, pas une seconde table d'effets à maintenir séparément du catalogue d'objets.
/// </summary>
public sealed class MonsterCareService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    private const int GiveItemExperience = 20;
    private const int MaxEv = 252;

    /// <summary>
    /// Voir Docs/Idees.md — "un objet type Fruit de force applique un bonus de stat permanent" :
    /// réutilise "Élixir de force" (voir <c>ProfessionCatalogSeeder</c>, déjà craftable par
    /// l'Alchimiste mais jusqu'ici sans aucun effet mécanique une fois obtenu) plutôt que
    /// d'inventer un nouvel objet — cohérent avec son nom/sa description ("décuple ... la force
    /// musculaire"). Bonus d'EV permanent sur l'Attaque, plafonné comme le gain d'EV de combat
    /// (voir <c>CombatService.MaxEv</c>).
    /// </summary>
    private static readonly Dictionary<string, int> GiveItemEvAttackBonusByName = new()
    {
        ["Élixir de force"] = 10,
    };

    public async Task<MonsterInstanceData> GiveItemAsync(GiveItemToMonsterRequest request, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(request.SessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var monster = await db.Monsters.FirstOrDefaultAsync(m => m.Id == request.MonsterId, ct)
            ?? throw new AccountOperationException("Créature introuvable.");

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == monster.OwnerCharacterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Cette créature n'appartient pas à un personnage de ce compte.");

        var inventoryItem = await db.InventoryItems.FirstOrDefaultAsync(i => i.CharacterId == character.Id && i.ItemId == request.ItemId, ct)
            ?? throw new AccountOperationException("Vous ne possédez pas cet objet.");

        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == request.ItemId, ct)
            ?? throw new AccountOperationException("Objet introuvable.");

        inventoryItem.Quantity--;
        if (inventoryItem.Quantity <= 0)
        {
            db.InventoryItems.Remove(inventoryItem);
        }

        if (GiveItemEvAttackBonusByName.TryGetValue(item.Name, out var evBonus))
        {
            monster.EvAttack = Math.Min(MaxEv, monster.EvAttack + evBonus);
        }
        else
        {
            MonsterProgressionService.GrantExperience(monster, GiveItemExperience);
        }

        await MonsterEvolutionService.CheckAndApplyAsync(db, monster, ct);
        await db.SaveChangesAsync(ct);

        return ToMonsterInstanceData(monster);
    }

    /// <summary>Voir GDD/demande utilisateur — "on peut changer la compétence [passive] avec un objet" (Parchemin de Compétence, voir PassiveTalentCatalog).</summary>
    public async Task<MonsterInstanceData> RerollPassiveTalentAsync(RerollPassiveTalentRequest request, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(request.SessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var monster = await db.Monsters.FirstOrDefaultAsync(m => m.Id == request.MonsterId, ct)
            ?? throw new AccountOperationException("Créature introuvable.");

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == monster.OwnerCharacterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Cette créature n'appartient pas à un personnage de ce compte.");

        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == request.ItemId, ct)
            ?? throw new AccountOperationException("Objet introuvable.");

        if (item.Name != "Parchemin de Compétence")
        {
            throw new AccountOperationException($"{item.Name} ne peut pas être utilisé de cette façon.");
        }

        var inventoryItem = await db.InventoryItems.FirstOrDefaultAsync(i => i.CharacterId == character.Id && i.ItemId == request.ItemId, ct)
            ?? throw new AccountOperationException("Vous ne possédez pas cet objet.");

        inventoryItem.Quantity--;
        if (inventoryItem.Quantity <= 0)
        {
            db.InventoryItems.Remove(inventoryItem);
        }

        monster.PassiveTalent = PassiveTalentCatalog.RollRandom(Random.Shared);
        await db.SaveChangesAsync(ct);

        return ToMonsterInstanceData(monster);
    }

    /// <summary>Voir GDD/demande utilisateur — "ajoute un item pour changer les iv" (Pierre de Réinitialisation) : même schéma que <see cref="RerollPassiveTalentAsync"/> ci-dessus.</summary>
    public async Task<MonsterInstanceData> RerollIvAsync(RerollIvRequest request, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(request.SessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var monster = await db.Monsters.FirstOrDefaultAsync(m => m.Id == request.MonsterId, ct)
            ?? throw new AccountOperationException("Créature introuvable.");

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == monster.OwnerCharacterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Cette créature n'appartient pas à un personnage de ce compte.");

        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == request.ItemId, ct)
            ?? throw new AccountOperationException("Objet introuvable.");

        if (item.Name != "Pierre de Réinitialisation")
        {
            throw new AccountOperationException($"{item.Name} ne peut pas être utilisé de cette façon.");
        }

        var inventoryItem = await db.InventoryItems.FirstOrDefaultAsync(i => i.CharacterId == character.Id && i.ItemId == request.ItemId, ct)
            ?? throw new AccountOperationException("Vous ne possédez pas cet objet.");

        inventoryItem.Quantity--;
        if (inventoryItem.Quantity <= 0)
        {
            db.InventoryItems.Remove(inventoryItem);
        }

        MonsterIvRoller.RollInto(monster, Random.Shared);
        await db.SaveChangesAsync(ct);

        return ToMonsterInstanceData(monster);
    }

    /// <summary>Voir GDD/demande utilisateur — "Talents/capacités passives uniques par monstre (comme les 'natures' Pokémon, influençant les stats)" (Pierre de Nature) : même schéma que <see cref="RerollIvAsync"/> ci-dessus.</summary>
    public async Task<MonsterInstanceData> RerollNatureAsync(RerollNatureRequest request, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(request.SessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var monster = await db.Monsters.FirstOrDefaultAsync(m => m.Id == request.MonsterId, ct)
            ?? throw new AccountOperationException("Créature introuvable.");

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == monster.OwnerCharacterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Cette créature n'appartient pas à un personnage de ce compte.");

        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == request.ItemId, ct)
            ?? throw new AccountOperationException("Objet introuvable.");

        if (item.Name != "Pierre de Nature")
        {
            throw new AccountOperationException($"{item.Name} ne peut pas être utilisé de cette façon.");
        }

        var inventoryItem = await db.InventoryItems.FirstOrDefaultAsync(i => i.CharacterId == character.Id && i.ItemId == request.ItemId, ct)
            ?? throw new AccountOperationException("Vous ne possédez pas cet objet.");

        inventoryItem.Quantity--;
        if (inventoryItem.Quantity <= 0)
        {
            db.InventoryItems.Remove(inventoryItem);
        }

        monster.Nature = MonsterNatureCatalog.RollRandom(Random.Shared);
        await db.SaveChangesAsync(ct);

        return ToMonsterInstanceData(monster);
    }

    /// <summary>Voir GDD/demande utilisateur — "4 créatures maximum participent au combat".</summary>
    private const int MaxEquippedMonsters = 4;

    /// <summary>
    /// Voir GDD/demande utilisateur — "on doit équiper les monstres au lieu de juste les mettre
    /// avec soi via la pension (pas bien)" : équipe/déséquipe une créature à un vrai emplacement
    /// (0 à <see cref="MaxEquippedMonsters"/> - 1, assigné automatiquement au premier libre plutôt
    /// que choisi par le client), refusé au-delà de 4 pour forcer à en retirer une d'abord plutôt
    /// que de silencieusement en ignorer une au combat.
    /// </summary>
    public async Task<MonsterInstanceData> SetEquippedAsync(string sessionToken, Guid monsterId, bool equip, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var monster = await db.Monsters.FirstOrDefaultAsync(m => m.Id == monsterId, ct)
            ?? throw new AccountOperationException("Créature introuvable.");

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == monster.OwnerCharacterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Cette créature n'appartient pas à un personnage de ce compte.");

        if (equip)
        {
            if (monster.EquippedSlot is null)
            {
                var usedSlots = await db.Monsters
                    .Where(m => m.OwnerCharacterId == character.Id && m.EquippedSlot != null)
                    .Select(m => m.EquippedSlot!.Value)
                    .ToListAsync(ct);

                var freeSlot = Enumerable.Range(0, MaxEquippedMonsters).FirstOrDefault(slot => !usedSlots.Contains(slot), -1);
                if (freeSlot < 0)
                {
                    throw new AccountOperationException("L'équipe active est déjà complète (4 créatures maximum).");
                }

                monster.EquippedSlot = freeSlot;
            }
        }
        else
        {
            monster.EquippedSlot = null;
        }

        await db.SaveChangesAsync(ct);

        return ToMonsterInstanceData(monster);
    }

    private static MonsterInstanceData ToMonsterInstanceData(MonsterEntity entity) => new()
    {
        Id = entity.Id,
        SpeciesId = entity.SpeciesId,
        OwnerCharacterId = entity.OwnerCharacterId,
        Variant = entity.Variant,
        Nickname = entity.Nickname,
        Level = entity.Level,
        Experience = entity.Experience,
        Personality = entity.Personality,
        PassiveTalent = entity.PassiveTalent,
        Nature = entity.Nature,
        EquippedSlot = entity.EquippedSlot,
        EquippedWeaponItemId = entity.EquippedWeaponItemId,
        EquippedArmorItemId = entity.EquippedArmorItemId,
        EquippedAccessoryItemId = entity.EquippedAccessoryItemId,
        CapturedAtUtc = entity.CapturedAtUtc,
        PrestigeLevel = entity.PrestigeLevel,
        IvHealth = entity.IvHealth, IvAttack = entity.IvAttack, IvDefense = entity.IvDefense,
        IvSpeed = entity.IvSpeed, IvIntelligence = entity.IvIntelligence, IvResistance = entity.IvResistance,
        EvHealth = entity.EvHealth, EvAttack = entity.EvAttack, EvDefense = entity.EvDefense,
        EvSpeed = entity.EvSpeed, EvIntelligence = entity.EvIntelligence, EvResistance = entity.EvResistance,
        PrestHealth = entity.PrestHealth, PrestAttack = entity.PrestAttack, PrestDefense = entity.PrestDefense,
        PrestSpeed = entity.PrestSpeed, PrestIntelligence = entity.PrestIntelligence, PrestResistance = entity.PrestResistance,
    };
}
