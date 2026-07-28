using Aetheria.Database.Entities;

namespace Aetheria.Server.World;

/// <summary>
/// Progression de niveau des créatures capturées (voir GDD — "UI pour les montres : monter de
/// niveau, objets à donner"). Jusqu'ici les créatures ne gagnaient jamais de niveau (seul le
/// personnage en gagnait via <see cref="CharacterProgressionService"/>) — même formule simple
/// (XP requise au niveau N = N × 100) pour rester cohérent avec le reste du projet.
/// </summary>
public static class MonsterProgressionService
{
    private const int ExperiencePerLevel = 100;

    public static void GrantExperience(MonsterEntity monster, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        monster.Experience += amount;

        while (monster.Experience >= monster.Level * ExperiencePerLevel)
        {
            monster.Experience -= monster.Level * ExperiencePerLevel;
            monster.Level++;
        }
    }
}
