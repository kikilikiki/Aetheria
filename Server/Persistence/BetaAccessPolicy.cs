using Aetheria.Shared;
using Aetheria.Shared.Enums;

namespace Aetheria.Server.Persistence;

/// <summary>
/// Bêta fermée (voir demande utilisateur — « les membres avec le grade Joueur / VIP ne peuvent
/// pas rejoindre ; seuls Testeur, Ami, Modérateur, Fondateur et les comptes admin le peuvent »).
/// Le contrôle s'applique à la connexion (<see cref="AccountService.LoginAsync"/>) et à l'entrée
/// en jeu (<c>PlayerSession.HandleEnterWorld</c>, filet pour un jeton encore valide émis avant
/// l'activation). Se désactive au lancement public en posant la variable d'environnement
/// <c>AETHERIA_CLOSED_BETA=false</c> (absente ou toute autre valeur ⇒ bêta fermée active).
/// </summary>
public static class BetaAccessPolicy
{
    public static bool IsClosedBeta => !string.Equals(
        Environment.GetEnvironmentVariable("AETHERIA_CLOSED_BETA"), "false", StringComparison.OrdinalIgnoreCase);

    /// <summary>Grades autorisés à se connecter pendant la bêta fermée (identiques aux grades
    /// éligibles au parrainage — voir <c>ReferralService.IsEligible</c>).</summary>
    public static bool CanConnect(bool isAdmin, UserRank rank) =>
        !IsClosedBeta
        || isAdmin
        || rank is UserRank.Testeur or UserRank.Ami or UserRank.Moderateur or UserRank.Fondateur;

    public static string DeniedMessage =>
        "Aetheria est en bêta fermée : ton compte n'a pas encore accès au jeu. "
        + $"Deviens bêta-testeur sur {GameInfo.WebsiteUrl}/beta.";
}
