using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>Voir GDD/demande utilisateur — "Défis hebdomadaires" (contenu end-game), avec des défis mensuels en plus. Progression calculée côté serveur à partir d'un instantané de la statistique concernée pris au début de chaque période (voir <c>Server/World/ChallengeService</c>).</summary>
public sealed record ChallengeDefinition(string Key, string Name, string Description, ChallengePeriod Period, ChallengeStatKind StatKind, long TargetValue, long RewardGold);

public static class ChallengeCatalog
{
    public static IReadOnlyList<ChallengeDefinition> All { get; } =
    [
        new("chasseur_semaine", "Chasseur de la semaine", "Capturez 5 créatures.", ChallengePeriod.Weekly, ChallengeStatKind.MonstersCaptured, 5, 300),
        new("duelliste_semaine", "Duelliste de la semaine", "Gagnez 5 combats PvP.", ChallengePeriod.Weekly, ChallengeStatKind.PvpWins, 5, 300),
        new("artisan_semaine", "Artisan de la semaine", "Fabriquez 10 objets.", ChallengePeriod.Weekly, ChallengeStatKind.ItemsCrafted, 10, 300),
        new("guerrier_semaine", "Guerrier de la semaine", "Gagnez 15 combats contre des créatures sauvages.", ChallengePeriod.Weekly, ChallengeStatKind.FightsWon, 15, 300),

        new("chasseur_mois", "Chasseur du mois", "Capturez 20 créatures.", ChallengePeriod.Monthly, ChallengeStatKind.MonstersCaptured, 20, 1500),
        new("duelliste_mois", "Duelliste du mois", "Gagnez 20 combats PvP.", ChallengePeriod.Monthly, ChallengeStatKind.PvpWins, 20, 1500),
        new("artisan_mois", "Artisan du mois", "Fabriquez 40 objets.", ChallengePeriod.Monthly, ChallengeStatKind.ItemsCrafted, 40, 1500),
        new("guerrier_mois", "Guerrier du mois", "Gagnez 60 combats contre des créatures sauvages.", ChallengePeriod.Monthly, ChallengeStatKind.FightsWon, 60, 1500),
    ];

    public static ChallengeDefinition? Find(string key) => All.FirstOrDefault(c => c.Key == key);
}
