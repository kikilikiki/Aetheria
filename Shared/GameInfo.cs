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

    /// <summary>Slogan officiel — affiché notamment en pied de chaque devlog Discord, à côté de la version.</summary>
    public const string Slogan = "🌟 AETHERIA — TON AVENTURE. TES MONSTRES. TA LÉGENDE. 🌟";

    /// <summary>Version courante du jeu (SemVer).</summary>
    public const string Version = "0.3.2";

    /// <summary>Port TCP par défaut utilisé par le serveur de jeu.</summary>
    public const int DefaultGamePort = 7777;

    /// <summary>Port HTTP par défaut de l'API de compte (inscription/connexion), utilisée par le Launcher.</summary>
    public const int DefaultAccountApiPort = 7778;

    /// <summary>Délai avant de passer automatiquement le tour d'un joueur qui n'agit pas (voir GDD/demande utilisateur — "augmenter le timer a 60sec"), en secondes.</summary>
    public const int CombatTurnTimeoutSeconds = 60;

    /// <summary>Délai avant de résoudre automatiquement un butin non entièrement réclamé (voir GDD/demande utilisateur — "augmenter le temps"), en secondes.</summary>
    public const int LootChoiceTimeoutSeconds = 20;

    /// <summary>
    /// Voir GDD/demande utilisateur — "une page A propos avec un bouton pour aller sur les CGU
    /// du site" (voir Launcher, panneau À propos). Pointe vers la page CGU du portail web
    /// (<c>Aetheria.Web</c>, déployé sur Render) — voir <c>Docs/Deploiement-Web.md</c> ;
    /// remplacer le sous-domaine si l'URL Render définitive diffère.
    /// </summary>
    public const string TermsOfServiceUrl = "https://aetheria-f7zo.onrender.com/cgu";

    /// <summary>Adresse du portail web (site vitrine + comptes + candidatures bêta).</summary>
    public const string WebsiteUrl = "https://aetheria-f7zo.onrender.com";

    // Réseaux officiels — partagés par le site (Aetheria.Web) et le Launcher (panneau « À propos »).
    public const string DiscordInviteUrl = "https://discord.gg/8NqXPsg7gE";
    public const string TikTokUrl = "https://www.tiktok.com/@aetheriafr";
    public const string YouTubeUrl = "https://www.youtube.com/channel/UClmvwFtRxYnfaqmxyxIXqgw";
    public const string ContactEmail = "aetheria.devteam@hotmail.com";
}
