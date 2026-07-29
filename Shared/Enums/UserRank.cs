namespace Aetheria.Shared.Enums;

/// <summary>
/// Grade communautaire d'un compte (voir GDD/demande utilisateur — "système de grade", affiché
/// dans le tchat et la liste des joueurs en ligne, assignable par un administrateur/fondateur).
/// Distinct de <c>UserEntity.IsAdmin</c> (permission technique de l'AdminPanel/Launcher) : le
/// grade est avant tout un affichage/statut communautaire. Assigné à l'utilisateur (pas au
/// personnage) : s'applique donc à tous ses personnages (voir GDD/demande utilisateur).
/// </summary>
public enum UserRank
{
    Joueur,
    VIP,
    Testeur,
    Ami,
    Moderateur,
    Fondateur,
}
