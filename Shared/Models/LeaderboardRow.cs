namespace Aetheria.Shared.Models;

/// <summary>Une ligne de classement : un personnage et son score dans la catégorie demandée.</summary>
public sealed record LeaderboardRow(string CharacterName, long Score);
