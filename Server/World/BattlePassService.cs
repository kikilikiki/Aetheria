using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Models.BattlePass;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Passe de Niveau (voir GDD/demande utilisateur — "un pass de niveaux de joueur ou chaque xp que
/// tu gagne est ajouté dedans aussi ou chaque passage te fait gagner quelque chose") : progression
/// alimentée par la même XP que <see cref="CharacterProgressionService"/> (voir les appels
/// jumeaux dans <c>QuestService.CompleteAsync</c> et <c>PartyService.GrantSharedExperienceAsync</c>,
/// les deux seules sources d'XP réellement gagnée en jeu — l'XP donnée manuellement par un admin
/// via <c>/givexp</c> n'alimente volontairement pas le pass). Même formule simple que les autres
/// systèmes de niveau du projet (XP requise au niveau N = N × 100).
///
/// Chaque palier (jusqu'à <see cref="MaxRewardLevel"/>) octroie automatiquement une récompense
/// gratuite, et une seconde bien plus généreuse si le pass premium est actif (voir
/// <see cref="PurchasePremiumAsync"/> — payant en gemmes, pas de vraie passerelle de paiement réel,
/// même limite que <c>PremiumService</c>). Voir GDD/demande utilisateur — "ne pas donner d'item
/// réservé aux admin ni de monstre mais rare" : les deux réserves d'objets ci-dessous ne
/// contiennent que des objets <c>IsObtainable</c> de rareté Commun/PeuCommun (gratuit) ou jusqu'à
/// Rare (premium) — voir Docs/Items.md, jamais les objets marqués "ADMIN UNIQUEMENT", jamais de
/// créature.
/// </summary>
public static class BattlePassService
{
    public const int ExperiencePerLevel = 100;
    public const int MaxRewardLevel = 50;
    public const long PremiumCostGems = 500;

    /// <summary>Voir demande utilisateur — "dans le pass premium au palier 10 ajoute l'obtention du titre 'premier arrivé premier servie'" : réutilise le système de titres existant (voir TitleCatalog/CharacterTitleEntity/ProfileService.ActiveTitle).</summary>
    private const int PremiumTitleLevel = 10;
    private const string PremiumTitleKey = "Premier arrivé premier servi";

    /// <summary>Récompense objet gratuite tous les 5 paliers (Commun/PeuCommun — voir Docs/Items.md).</summary>
    private static readonly string[] FreeMilestoneItems =
    [
        "Grande potion de soin", "Potion de soin supérieure", "Antidote",
        "Épée de fer", "Armure de fer", "Épée d'argent", "Armure d'Argent",
    ];

    /// <summary>Récompense objet premium tous les 5 paliers (jusqu'à Rare — voir Docs/Items.md).</summary>
    private static readonly string[] PremiumMilestoneItems =
    [
        "Anneau de Chance", "Anneau du Mage", "Collier Royal", "Épée en or",
        "Épée Royale", "Lance Royale", "Armure d'or", "Armure Royale",
        "Potion d'expérience", "Potion de fortune", "Potion de chance", "Élixir de force",
    ];

    public static async Task GrantExperienceAsync(AetheriaDbContext db, CharacterEntity character, long amount, CancellationToken ct = default)
    {
        if (amount <= 0)
        {
            return;
        }

        character.BattlePassXp += amount;

        while (character.BattlePassXp >= character.BattlePassLevel * (long)ExperiencePerLevel)
        {
            character.BattlePassXp -= character.BattlePassLevel * (long)ExperiencePerLevel;
            character.BattlePassLevel++;

            if (character.BattlePassLevel > MaxRewardLevel)
            {
                continue; // Au-delà du dernier palier catalogué : le niveau continue de monter, sans récompense supplémentaire.
            }

            await GrantFreeRewardAsync(db, character, character.BattlePassLevel, ct);
            if (character.BattlePassHasPremium)
            {
                await GrantPremiumRewardAsync(db, character, character.BattlePassLevel, ct);
                character.BattlePassLastPremiumRewardLevel = character.BattlePassLevel;
            }
        }
    }

    /// <summary>
    /// Débloque le pass premium contre des gemmes et rattrape immédiatement les récompenses
    /// premium des paliers déjà atteints (voir GDD/demande utilisateur) — sans quoi acheter le
    /// pass après avoir déjà bien progressé ne rapporterait rien avant le PROCHAIN palier.
    /// </summary>
    public static async Task<BattlePassStatus> PurchasePremiumAsync(AetheriaDbContext db, UserEntity user, CharacterEntity character, CancellationToken ct = default)
    {
        if (character.BattlePassHasPremium)
        {
            throw new AccountOperationException("Le pass premium est déjà actif sur ce personnage.");
        }

        if (user.Gems < PremiumCostGems)
        {
            throw new AccountOperationException($"Pas assez de gemmes (coût : {PremiumCostGems} gemmes).");
        }

        user.Gems -= PremiumCostGems;
        character.BattlePassHasPremium = true;

        for (var level = Math.Max(2, character.BattlePassLastPremiumRewardLevel + 1); level <= character.BattlePassLevel && level <= MaxRewardLevel; level++)
        {
            await GrantPremiumRewardAsync(db, character, level, ct);
        }

        character.BattlePassLastPremiumRewardLevel = Math.Min(character.BattlePassLevel, MaxRewardLevel);

        await db.SaveChangesAsync(ct);
        return ToStatus(character);
    }

    private static async Task GrantFreeRewardAsync(AetheriaDbContext db, CharacterEntity character, int level, CancellationToken ct)
    {
        if (level % 5 == 0)
        {
            await GrantItemAsync(db, character, FreeMilestoneItems[(level / 5 - 1) % FreeMilestoneItems.Length], ct);
            await GrantGemsAsync(db, character, 10 * (level / 5), ct);
        }
        else
        {
            character.Gold += level * 20L;
        }
    }

    private static async Task GrantPremiumRewardAsync(AetheriaDbContext db, CharacterEntity character, int level, CancellationToken ct)
    {
        if (level % 5 == 0)
        {
            await GrantItemAsync(db, character, PremiumMilestoneItems[(level / 5 - 1) % PremiumMilestoneItems.Length], ct);
            await GrantGemsAsync(db, character, 20 * (level / 5), ct);
        }
        else
        {
            character.Gold += level * 40L;
            await GrantGemsAsync(db, character, 5, ct);
        }

        if (level == PremiumTitleLevel)
        {
            await GrantTitleAsync(db, character, PremiumTitleKey, ct);
        }
    }

    private static async Task GrantTitleAsync(AetheriaDbContext db, CharacterEntity character, string titleKey, CancellationToken ct)
    {
        var alreadyOwned = await db.CharacterTitles.AnyAsync(t => t.CharacterId == character.Id && t.TitleKey == titleKey, ct);
        if (!alreadyOwned)
        {
            db.CharacterTitles.Add(new CharacterTitleEntity { Id = Guid.NewGuid(), CharacterId = character.Id, TitleKey = titleKey });
        }
    }

    private static async Task GrantItemAsync(AetheriaDbContext db, CharacterEntity character, string itemName, CancellationToken ct)
    {
        var item = await db.Items.FirstOrDefaultAsync(i => i.Name == itemName, ct);
        if (item is null)
        {
            return; // Catalogue introuvable (base non re-seedée) — ignoré plutôt que de faire échouer toute la remise de récompense.
        }

        await InventoryStackingService.AddQuantityAsync(db, character.Id, item.Id, 1, item.MaxStackSize <= 0 ? 99 : item.MaxStackSize, ct);
    }

    private static async Task GrantGemsAsync(AetheriaDbContext db, CharacterEntity character, long amount, CancellationToken ct)
    {
        if (amount <= 0)
        {
            return;
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == character.UserId, ct);
        if (user is not null)
        {
            user.Gems += amount;
        }
    }

    public static BattlePassStatus ToStatus(CharacterEntity character) => new()
    {
        Level = character.BattlePassLevel,
        Experience = character.BattlePassXp,
        ExperienceForNextLevel = character.BattlePassLevel * (long)ExperiencePerLevel,
        HasPremium = character.BattlePassHasPremium,
        PremiumCostGems = character.BattlePassHasPremium ? null : PremiumCostGems,
        MaxRewardLevel = MaxRewardLevel,
    };
}
