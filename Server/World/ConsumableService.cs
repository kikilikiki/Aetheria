using Aetheria.Database.Context;
using Aetheria.Server.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Voir GDD/demande utilisateur — "ajoute des consommables pour booster la luck l'xp la money" :
/// consomme une potion de boost identifiée par nom (voir <see cref="TemporaryBoostService"/>),
/// utilisable via <c>/use &lt;idObjet&gt;</c> (PlayerSession) ou <c>POST /api/inventory/use</c>.
/// </summary>
public sealed class ConsumableService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    private static readonly Dictionary<string, Action<Database.Entities.CharacterEntity>> BoostPotions = new()
    {
        ["Potion d'expérience"] = c => c.XpBoostExpiresAtUtc = DateTime.UtcNow + TemporaryBoostService.BoostDuration,
        ["Potion de fortune"] = c => c.GoldBoostExpiresAtUtc = DateTime.UtcNow + TemporaryBoostService.BoostDuration,
        ["Potion de chance"] = c => c.LuckBoostExpiresAtUtc = DateTime.UtcNow + TemporaryBoostService.BoostDuration,
    };

    public async Task<string> UseAsync(string sessionToken, Guid characterId, int itemId, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");

        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == itemId, ct)
            ?? throw new AccountOperationException("Objet introuvable.");

        if (!BoostPotions.TryGetValue(item.Name, out var apply))
        {
            throw new AccountOperationException($"{item.Name} ne peut pas être utilisé de cette façon.");
        }

        var stack = await db.InventoryItems.FirstOrDefaultAsync(i => i.CharacterId == characterId && i.ItemId == itemId, ct)
            ?? throw new AccountOperationException($"Vous n'avez pas de {item.Name}.");

        stack.Quantity--;
        if (stack.Quantity <= 0)
        {
            db.InventoryItems.Remove(stack);
        }

        apply(character);
        await db.SaveChangesAsync(ct);

        return $"{item.Name} utilisé(e) — effet actif pendant {TemporaryBoostService.BoostDuration.TotalMinutes:0} minutes.";
    }
}
