using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Saisons (voir <c>Docs/GameDesign.md</c> — section Saisons). Ne gère que le cycle
/// actif/inactif et la numérotation ; le contenu ajouté à chaque saison (monstres, donjons,
/// cosmétiques, passe saison) est un travail de contenu qui reste à faire, pas une
/// responsabilité de ce service.
/// </summary>
public sealed class SeasonService(AetheriaDbContext db)
{
    public async Task<SeasonEntity> GetCurrentAsync(CancellationToken ct = default)
        => await db.Seasons.FirstOrDefaultAsync(s => s.IsActive, ct)
            ?? throw new AccountOperationException("Aucune saison active.");

    public async Task<SeasonEntity> StartNextSeasonAsync(CancellationToken ct = default)
    {
        var current = await db.Seasons.FirstOrDefaultAsync(s => s.IsActive, ct);
        var nextNumber = 1;

        if (current is not null)
        {
            current.IsActive = false;
            current.EndedAtUtc = DateTime.UtcNow;
            nextNumber = current.Number + 1;
        }

        var season = new SeasonEntity { Number = nextNumber, IsActive = true };
        db.Seasons.Add(season);
        await db.SaveChangesAsync(ct);

        return season;
    }
}
