using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Database.Services;

/// <summary>
/// Rédemption d'un <see cref="GiftCodeEntity"/> (voir demande utilisateur — codes cadeaux à saisir
/// sur le site et dans le Launcher, dont le Fondateur choisit le contenu : gemmes, or, créature,
/// ou texte libre). Partagé par le portail web et le serveur de jeu.
/// </summary>
public static class GiftCodeRedeemer
{
    // Miroir de MonsterProgressionService.MaxLevel (côté Server, non référençable ici).
    private const int MaxMonsterLevel = 150;

    public sealed record Result(bool Success, string Message);

    /// <summary>Normalise un code saisi : majuscules, alphanumérique.</summary>
    public static string Normalize(string raw) =>
        new(raw.Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    /// <summary>
    /// <paramref name="characterId"/> : personnage qui reçoit l'or / la créature (le Launcher le
    /// passe toujours ; le site le passe si le compte a choisi un personnage). Si le code accorde
    /// de l'or ou une créature et qu'aucun personnage n'est disponible, la rédemption est refusée
    /// (rien n'est consommé) plutôt que d'accorder une récompense partielle.
    /// </summary>
    public static async Task<Result> RedeemAsync(AetheriaDbContext db, Guid userId, string rawCode, string source, Guid? characterId = null, CancellationToken ct = default)
    {
        var code = Normalize(rawCode);
        if (code.Length == 0)
        {
            return new Result(false, "Saisis un code.");
        }

        var giftCode = await db.GiftCodes.FirstOrDefaultAsync(c => c.Code == code, ct);
        if (giftCode is null || !giftCode.IsActive)
        {
            return new Result(false, "Code invalide.");
        }

        if (giftCode.ExpiresAtUtc is { } expiry && expiry < DateTime.UtcNow)
        {
            return new Result(false, "Ce code a expiré.");
        }

        if (giftCode.MaxRedemptions is { } max && giftCode.RedemptionCount >= max)
        {
            return new Result(false, "Ce code a atteint sa limite d'utilisations.");
        }

        var already = await db.GiftCodeRedemptions.AnyAsync(r => r.GiftCodeId == giftCode.Id && r.UserId == userId, ct);
        if (already)
        {
            return new Result(false, "Tu as déjà utilisé ce code.");
        }

        var needsCharacter = giftCode.RewardGold != 0 || giftCode.RewardMonsterSpeciesId is not null;
        CharacterEntity? character = null;
        if (needsCharacter)
        {
            character = characterId is { } cid
                ? await db.Characters.FirstOrDefaultAsync(c => c.Id == cid && c.UserId == userId, ct)
                : await db.Characters.Where(c => c.UserId == userId).OrderBy(c => c.CreatedAtUtc).FirstOrDefaultAsync(ct);

            if (character is null)
            {
                return new Result(false, "Ce code offre de l'or ou une créature : utilise-le depuis le jeu (Launcher), une fois un personnage créé.");
            }
        }

        var granted = new List<string>();

        if (giftCode.RewardGems != 0)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user is not null)
            {
                user.Gems = Math.Max(0, user.Gems + giftCode.RewardGems);
                granted.Add($"+{giftCode.RewardGems} gemme(s)");
            }
        }

        if (giftCode.RewardGold != 0 && character is not null)
        {
            character.Gold = Math.Max(0, character.Gold + giftCode.RewardGold);
            granted.Add($"+{giftCode.RewardGold} or");
        }

        if (giftCode.RewardMonsterSpeciesId is { } speciesId && character is not null)
        {
            var species = await db.MonsterSpecies.FirstOrDefaultAsync(s => s.Id == speciesId, ct);
            if (species is null)
            {
                return new Result(false, "Ce code référence une espèce de créature inconnue — préviens un administrateur.");
            }

            var level = Math.Clamp(giftCode.RewardMonsterLevel, 1, MaxMonsterLevel);
            var monster = new MonsterEntity
            {
                Id = Guid.NewGuid(),
                OwnerCharacterId = character.Id,
                SpeciesId = species.Id,
                Variant = giftCode.RewardMonsterVariant,
                Nickname = species.Name,
                Level = level,
                Nature = MonsterNatureCatalog.RollRandom(Random.Shared),
                IvHealth = Random.Shared.Next(0, 32),
                IvAttack = Random.Shared.Next(0, 32),
                IvDefense = Random.Shared.Next(0, 32),
                IvSpeed = Random.Shared.Next(0, 32),
                IvIntelligence = Random.Shared.Next(0, 32),
                IvResistance = Random.Shared.Next(0, 32),
            };
            db.Monsters.Add(monster);
            granted.Add($"{species.Name} niv. {level}" + (giftCode.RewardMonsterVariant != MonsterVariant.Normal ? $" ({giftCode.RewardMonsterVariant})" : ""));
        }

        db.GiftCodeRedemptions.Add(new GiftCodeRedemptionEntity
        {
            Id = Guid.NewGuid(),
            GiftCodeId = giftCode.Id,
            Code = giftCode.Code,
            UserId = userId,
            Source = source,
        });
        giftCode.RedemptionCount++;

        await db.SaveChangesAsync(ct);

        var parts = new List<string>();
        if (granted.Count > 0)
        {
            parts.Add(string.Join(", ", granted));
        }
        if (!string.IsNullOrWhiteSpace(giftCode.Description))
        {
            parts.Add(giftCode.Description);
        }

        var summary = parts.Count > 0 ? string.Join(" — ", parts) : "récompense enregistrée";
        return new Result(true, $"Code accepté ! {summary}");
    }
}
