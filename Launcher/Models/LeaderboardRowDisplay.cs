namespace Aetheria.Launcher.Models;

/// <summary>Ligne de classement affichée dans le Launcher (voir GDD/demande utilisateur — "un bouton pour le leaderboard en jeu et sur le launcher"), avec le rang précalculé (l'API ne renvoie que le nom/score, déjà triés).</summary>
public sealed record LeaderboardRowDisplay(int Rank, string CharacterName, long Score);
