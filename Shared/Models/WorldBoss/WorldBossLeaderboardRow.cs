namespace Aetheria.Shared.Models.WorldBoss;

/// <summary>Une ligne du classement de dégâts du boss mondial (voir GDD/demande utilisateur — "leaderboard... du boss actuel et de toujours").</summary>
public sealed record WorldBossLeaderboardRow(string CharacterName, long TotalDamage);
