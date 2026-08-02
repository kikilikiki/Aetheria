using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Attribution du tout premier compagnon d'un personnage (voir <c>Docs/GameDesign.md</c> —
/// scène d'introduction façon "choix du starter"). Contrairement à <see cref="CaptureService"/>,
/// il n'y a ni combat préalable ni jet de réussite : un choix garanti parmi les créatures
/// communes, une seule fois par personnage (bloqué dès qu'il possède déjà une créature).
/// </summary>
public sealed class StarterService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    private const int StarterLevel = 5;

    public async Task<StarterChoiceResponse> ChooseStarterAsync(StarterChoiceRequest request, CancellationToken ct = default)
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

        var alreadyHasMonster = await db.Monsters.AnyAsync(m => m.OwnerCharacterId == character.Id, ct);
        if (alreadyHasMonster)
        {
            throw new AccountOperationException("Ce personnage a déjà choisi son premier compagnon.");
        }

        var species = await db.MonsterSpecies
            .FirstOrDefaultAsync(s => s.Id == request.SpeciesId && s.BaseRarity == Rarity.Commun, ct);
        if (species is null)
        {
            throw new AccountOperationException("Ce monstre n'est pas disponible comme premier compagnon.");
        }

        var monster = new MonsterEntity
        {
            Id = Guid.NewGuid(),
            OwnerCharacterId = character.Id,
            SpeciesId = species.Id,
            Variant = MonsterVariant.Normal,
            Nickname = species.Name,
            Level = StarterLevel,
            EquippedSlot = 0,
            // Voir GDD/demande utilisateur — "Compétences passives" : tirée une fois pour toutes,
            // comme pour une capture (voir CaptureService.AttemptCaptureAsync).
            PassiveTalent = PassiveTalentCatalog.RollRandom(Random.Shared),
            Nature = MonsterNatureCatalog.RollRandom(Random.Shared),
        };

        // Voir GDD/demande utilisateur — "ajoute un random iv".
        MonsterIvRoller.RollInto(monster, Random.Shared);

        db.Monsters.Add(monster);
        await db.SaveChangesAsync(ct);

        await new AchievementService(db).UnlockAsync(character.UserId, "premier_compagnon", ct);

        return new StarterChoiceResponse
        {
            Success = true,
            MonsterId = monster.Id,
            Message = $"{species.Name} rejoint votre aventure !",
        };
    }
}
