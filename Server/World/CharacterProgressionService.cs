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

    /// <summary>
    /// Voir GDD/demande utilisateur — "recompenses de niveau de personnage en plus du pass" : or
    /// accordé à chaque niveau multiple de <see cref="RewardLevelInterval"/>, en plus (pas à la
    /// place) des paliers du passe de combat (voir BattlePassService), qui restent inchangés. Le
    /// gain est déjà surfacé au joueur sans code client supplémentaire — voir
    /// Client/Program.cs ApplyProfileUpdate, qui affiche un toast dès que l'or/le niveau du profil
    /// augmente, quelle qu'en soit la source.
    /// </summary>
    private const int RewardLevelInterval = 5;
    private const int RewardGoldPerInterval = 100;

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

            if (character.Level % RewardLevelInterval == 0)
            {
                character.Gold += RewardGoldPerInterval * (character.Level / RewardLevelInterval);
            }
        }
    }
}
