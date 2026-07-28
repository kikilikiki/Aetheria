namespace Aetheria.Shared;

/// <summary>
/// Informations de version du jeu, partagées par tous les modules (Client, Server, Launcher).
/// Le Launcher s'appuie sur <see cref="Version"/> pour détecter si une mise à jour est nécessaire,
/// et le Client/Server peuvent vérifier leur compatibilité au moment de la connexion.
/// </summary>
public static class GameInfo
{
    /// <summary>Nom commercial du jeu.</summary>
    public const string Name = "Aetheria";

    /// <summary>Version courante du jeu (SemVer).</summary>
    public const string Version = "0.1.0";

    /// <summary>Port TCP par défaut utilisé par le serveur de jeu.</summary>
    public const int DefaultGamePort = 7777;

    /// <summary>Port HTTP par défaut de l'API de compte (inscription/connexion), utilisée par le Launcher.</summary>
    public const int DefaultAccountApiPort = 7778;

    /// <summary>Délai avant de passer automatiquement le tour d'un joueur qui n'agit pas (voir GDD/demande utilisateur — "timer de 10 secondes entre chaque tour"), en secondes.</summary>
    public const int CombatTurnTimeoutSeconds = 10;

    /// <summary>Délai avant de résoudre automatiquement un butin non entièrement réclamé (voir GDD/demande utilisateur — "timer de 10 secondes pour le choix des gains"), en secondes.</summary>
    public const int LootChoiceTimeoutSeconds = 10;
}
