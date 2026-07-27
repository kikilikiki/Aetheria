using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Shared.Models.Account;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.Persistence;

/// <summary>Création de personnage (voir <c>Docs/GameDesign.md</c> — "Au début du jeu, chaque joueur choisit un royaume").</summary>
public sealed class CharacterService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    public async Task<CharacterSummary> CreateAsync(CreateCharacterRequest request, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(request.SessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var nameTaken = await db.Characters.AnyAsync(c => c.Name == request.Name, ct);
        if (nameTaken)
        {
            throw new AccountOperationException("Ce nom de personnage est déjà pris.");
        }

        var character = new CharacterEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name,
            Class = request.Class,
            Kingdom = request.Kingdom,
        };

        db.Characters.Add(character);
        db.Statistics.Add(new StatisticsEntity
        {
            Id = Guid.NewGuid(),
            CharacterId = character.Id,
        });

        await db.SaveChangesAsync(ct);

        return new CharacterSummary { Id = character.Id, Name = character.Name, Level = character.Level };
    }
}
