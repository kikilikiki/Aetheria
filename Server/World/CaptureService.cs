using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Mécanique de capture (voir <c>Docs/GameDesign.md</c> — section Capture des Créatures) :
/// combattre puis affaiblir la créature restent la responsabilité du futur système de combat
/// tactique ; ce service prend en entrée le résultat de ce combat (<c>TargetHealthPercent</c>)
/// et applique les étapes 3 et 4 (objet spécial + jet de maîtrise).
/// </summary>
public sealed class CaptureService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    private static readonly Random Random = new();

    public async Task<CaptureAttemptResponse> AttemptCaptureAsync(CaptureAttemptRequest request, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(request.SessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var character = await db.Characters
            .FirstOrDefaultAsync(c => c.Id == request.CharacterId && c.UserId == userId, ct);
        if (character is null)
        {
            throw new AccountOperationException("Personnage introuvable pour ce compte.");
        }

        var species = await db.MonsterSpecies.FirstOrDefaultAsync(s => s.Id == request.SpeciesId, ct);
        if (species is null)
        {
            throw new AccountOperationException("Espèce de créature inconnue.");
        }

        var captureItem = await db.InventoryItems
            .Include(i => i.Item)
            .FirstOrDefaultAsync(i =>
                i.CharacterId == character.Id && i.ItemId == request.CaptureItemId && i.Quantity > 0, ct);

        if (captureItem?.Item is not { ItemType: ItemType.ObjetDeCapture })
        {
            throw new AccountOperationException("Objet de capture manquant dans l'inventaire.");
        }

        // Voir GDD/demande utilisateur — variantes de créature (voir MonsterVariantCatalog) :
        // compense le bonus de statistiques par un taux de capture réduit, appliqué par-dessus
        // la pénalité de rareté existante.
        var successChance = ComputeSuccessChance(request.TargetHealthPercent, species.BaseRarity)
            * MonsterVariantCatalog.Get(request.Variant).CaptureRateMultiplier;
        successChance = Math.Clamp(successChance, 0.02, 0.95);
        var success = Random.NextDouble() < successChance;

        // L'objet de capture est consommé que la tentative réussisse ou non.
        captureItem.Quantity -= 1;
        if (captureItem.Quantity <= 0)
        {
            db.InventoryItems.Remove(captureItem);
        }

        if (!success)
        {
            await db.SaveChangesAsync(ct);
            return new CaptureAttemptResponse { Success = false, Message = $"{species.Name} sauvage s'est échappé !" };
        }

        var monster = new MonsterEntity
        {
            Id = Guid.NewGuid(),
            OwnerCharacterId = character.Id,
            SpeciesId = species.Id,
            Variant = request.Variant,
            Nickname = species.Name,
            Level = 1,
            // Voir GDD/demande utilisateur — "Compétences passives" : tirée une fois pour toutes à la capture.
            PassiveTalent = PassiveTalentCatalog.RollRandom(Random),
            // Voir GDD/demande utilisateur — "Talents/capacités passives uniques par monstre (comme les 'natures' Pokémon)" : tirée une fois pour toutes à la capture, comme le talent passif ci-dessus.
            Nature = MonsterNatureCatalog.RollRandom(Random),
        };

        // Voir GDD/demande utilisateur — "ajoute un random iv" : tirées une fois pour toutes à la capture, comme le talent passif ci-dessus.
        MonsterIvRoller.RollInto(monster, Random);

        db.Monsters.Add(monster);

        // Voir GDD/demande utilisateur — "Défis hebdomadaires" (progression dérivée de cette statistique).
        var captureStats = await db.Statistics.FirstOrDefaultAsync(s => s.CharacterId == character.Id, ct);
        if (captureStats is not null)
        {
            captureStats.Monsters.MonstersCaptured++;
        }

        await db.SaveChangesAsync(ct);

        await new AchievementService(db).UnlockAsync(character.UserId, "premiere_capture", ct);

        return new CaptureAttemptResponse
        {
            Success = true,
            CapturedMonsterId = monster.Id,
            Message = $"{species.Name} a été capturé !",
        };
    }

    /// <summary>
    /// Formule de base : plus la créature est affaiblie, plus la capture a de chances de
    /// réussir ; les raretés élevées résistent davantage. À affiner quand le système complet
    /// de maîtrise par espèce sera en place (voir GDD — "posséder assez de maîtrise").
    /// </summary>
    private static double ComputeSuccessChance(int targetHealthPercent, Rarity rarity)
    {
        var healthFactor = 1.0 - Math.Clamp(targetHealthPercent, 0, 100) / 100.0;
        var rarityPenalty = rarity switch
        {
            Rarity.Commun => 0.0,
            Rarity.PeuCommun => 0.10,
            Rarity.Rare => 0.25,
            Rarity.Epique => 0.40,
            Rarity.Legendaire => 0.60,
            Rarity.Mythique => 0.75,
            _ => 0.0,
        };

        return Math.Clamp(healthFactor - rarityPenalty, 0.05, 0.95);
    }
}
