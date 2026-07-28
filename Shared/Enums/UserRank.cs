namespace Aetheria.Shared.Enums;

/// <summary>
/// Grade communautaire d'un compte (voir GDD/demande utilisateur — "système de grade", affiché
/// dans le tchat et la liste des joueurs en ligne, assignable par un administrateur). Distinct de
/// <c>UserEntity.IsAdmin</c> (permission technique de l'AdminPanel) : un grade est avant tout un
/// affichage/statut communautaire, même si <c>Administrateur</c> ici correspond en pratique aux
/// comptes admin.
/// </summary>
public enum UserRank
{
    Joueur,
    Veteran,
    Moderateur,
    Administrateur,
}
