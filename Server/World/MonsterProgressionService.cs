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

    /// <summary>Voir GDD/demande utilisateur — limite de niveau à 150 pour les monstres, pas de limite pour les joueurs : plafond propre aux créatures uniquement (voir <see cref="CharacterProgressionService"/>, non plafonné).</summary>
    public const int MaxLevel = 150;

    public static void GrantExperience(MonsterEntity monster, int amount)
    {
        if (amount <= 0 || monster.Level >= MaxLevel)
        {
            return;
        }

        monster.Experience += amount;

        while (monster.Level < MaxLevel && monster.Experience >= monster.Level * ExperiencePerLevel)
        {
            monster.Experience -= monster.Level * ExperiencePerLevel;
            monster.Level++;

            // Voir Docs/Idees.md — Arbre de talents : +1 point par niveau gagné.
            monster.TalentPoints++;
        }

        if (monster.Level >= MaxLevel)
        {
            monster.Level = MaxLevel;
            monster.Experience = 0;
        }
    }
}
