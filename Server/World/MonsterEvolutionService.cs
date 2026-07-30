using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Évolution des créatures (voir GDD/demande utilisateur — "Évolution des monstres") : les champs
/// <c>MonsterSpeciesEntity.EvolvesIntoSpeciesId</c>/<c>EvolutionLevel</c> existaient déjà (éditables
/// depuis Aetheria.MonsterEditor) mais rien ne les exploitait encore — ce service est le
/// déclencheur manquant, appelé après tout gain de niveau d'une créature (voir
/// <c>CombatService.ApplyPveVictoryRewardsAsync</c>, <c>MonsterCareService.GiveItemAsync</c>).
/// </summary>
public static class MonsterEvolutionService
{
    /// <summary>Fait évoluer la créature si son espèce a une évolution configurée et que son niveau l'atteint désormais — silencieux sinon (pas d'évolution configurée, ou palier pas encore atteint).</summary>
    public static async Task<MonsterSpeciesEntity?> CheckAndApplyAsync(AetheriaDbContext db, MonsterEntity monster, CancellationToken ct = default)
    {
        var species = await db.MonsterSpecies.FirstOrDefaultAsync(s => s.Id == monster.SpeciesId, ct);
        if (species?.EvolvesIntoSpeciesId is not { } targetSpeciesId || monster.Level < species.EvolutionLevel)
        {
            return null;
        }

        var targetSpecies = await db.MonsterSpecies.FirstOrDefaultAsync(s => s.Id == targetSpeciesId, ct);
        if (targetSpecies is null)
        {
            return null;
        }

        // Un surnom personnalisé (différent du nom d'espèce d'origine) est conservé ; le surnom
        // par défaut (jamais renommé) suit l'évolution.
        if (monster.Nickname == species.Name)
        {
            monster.Nickname = targetSpecies.Name;
        }

        monster.SpeciesId = targetSpecies.Id;
        return targetSpecies;
    }
}
