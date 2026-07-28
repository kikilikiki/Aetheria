using Aetheria.Database.Entities;

namespace Aetheria.Server.World;

/// <summary>
/// Progression de niveau des personnages via l'XP de combat (voir <c>CombatService</c> —
/// récompenses de victoire PvE, et <c>PartyService.GrantSharedExperienceAsync</c>). Même formule
/// simple que <c>ProfessionService</c> (XP requise au niveau N = N × 100) pour rester cohérent.
/// </summary>
public static class CharacterProgressionService
{
    private const int ExperiencePerLevel = 100;

    public static void GrantExperience(CharacterEntity character, long amount)
    {
        if (amount <= 0)
        {
            return;
        }

        character.Experience += amount;

        while (character.Experience >= character.Level * ExperiencePerLevel)
        {
            character.Experience -= character.Level * ExperiencePerLevel;
            character.Level++;
        }
    }
}
