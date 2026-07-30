using Aetheria.Database.Context;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Voir GDD/demande utilisateur — "Exploration : îles volantes (monture volante requise) et îles
/// aquatiques (monture aquatique requise)". Simplification assumée (voir Docs/README.md) : pas de
/// nouvelle géographie/terrain dédiés (le moteur n'a pas de notion d'élévation/traversée d'eau,
/// voir <c>Client/World/WorldMap.cs</c>) — la monture dédiée est la condition d'accès réellement
/// vérifiée, matérialisée par un succès caché plutôt qu'un déplacement de carte fictif.
/// </summary>
public sealed class ExplorationService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    public async Task<string> VisitIslandAsync(VisitIslandRequest request, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(request.SessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == request.CharacterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");

        if (request.IslandKind == MountKind.Terrestre)
        {
            throw new AccountOperationException("Ce type d'île ne nécessite aucune monture spéciale.");
        }

        var ownedMountKeys = await db.Collections.Where(c => c.UserId == character.UserId && c.Category == "Monture").Select(c => c.CollectionKey).ToListAsync(ct);
        var hasRequiredMount = ownedMountKeys.Any(key => MountCatalog.Find(key)?.Kind == request.IslandKind);
        if (!hasRequiredMount)
        {
            var mountKindLabel = request.IslandKind == MountKind.Volant ? "volante" : "aquatique";
            throw new AccountOperationException($"Une monture {mountKindLabel} est nécessaire pour rejoindre cette île.");
        }

        var achievementKey = request.IslandKind == MountKind.Volant ? "explorateur_des_cieux" : "explorateur_des_flots";
        await new AchievementService(db).UnlockAsync(character.UserId, achievementKey, ct);

        return request.IslandKind == MountKind.Volant
            ? "Vous survolez une île volante, portés par votre monture."
            : "Vous rejoignez une île aquatique, portés par votre monture.";
    }
}
