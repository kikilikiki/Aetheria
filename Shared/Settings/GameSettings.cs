using System.Text.Json;

namespace Aetheria.Shared.Settings;

public enum KeyboardLayoutPreference
{
    Auto,
    Qwerty,
    Azerty,
}

/// <summary>
/// Préférences partagées entre le Launcher et le Client, persistées dans un fichier JSON commun
/// (voir GDD — disposition clavier détectée automatiquement mais réglable dans les deux).
/// </summary>
public sealed class GameSettings
{
    public KeyboardLayoutPreference KeyboardLayout { get; set; } = KeyboardLayoutPreference.Auto;

    /// <summary>
    /// Adresse (IP publique ou nom de domaine) du serveur Aetheria — voir GDD/demande utilisateur
    /// : "si on installe le jeu depuis un autre PC/wifi, on doit quand même pouvoir accéder au
    /// serveur hébergé chez [l'utilisateur]". "localhost" par défaut (développement local) ; à
    /// changer dans les Paramètres du Launcher pour se connecter à un serveur distant. Utilisé à
    /// la fois par le Launcher (API de compte) et transmis au Client (`--host`) pour la connexion
    /// TCP de jeu, afin que les deux ciblent toujours le même serveur.
    /// </summary>
    public string ServerHost { get; set; } = "localhost";

    /// <summary>
    /// Base URL complète (avec schéma, ex. "https://xxxx.ngrok-free.dev") pour l'API de compte
    /// (port 7778), à utiliser à la place de <c>http://{ServerHost}:7778</c> quand elle est
    /// renseignée — voir GDD/demande utilisateur : "pour se connecter le port ip etc sera
    /// prérempli et utilise ngrok". <see cref="ServerHost"/> reste utilisé tel quel pour la
    /// connexion TCP de jeu (port 7777, redirection de ports classique côté routeur — les
    /// tunnels ngrok TCP exigent une carte bancaire vérifiée sur le compte, non activée ici).
    /// Vide par défaut : aucun changement de comportement tant que ce champ n'est pas renseigné.
    /// </summary>
    public string? AccountApiBaseUrl { get; set; }

    /// <summary>Résout l'adresse effective de l'API de compte (voir <see cref="AccountApiBaseUrl"/>).</summary>
    public string ResolveAccountApiBaseUrl(int defaultPort) =>
        string.IsNullOrWhiteSpace(AccountApiBaseUrl) ? $"http://{ServerHost}:{defaultPort}" : AccountApiBaseUrl.TrimEnd('/');

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Aetheria", "settings.json");

    public static GameSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<GameSettings>(json);
                if (loaded is not null)
                {
                    return loaded;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Fichier absent, corrompu ou illisible : on retombe sur les valeurs par défaut.
        }

        return new GameSettings();
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Pas bloquant : la préférence reste active pour cette session, juste pas persistée.
        }
    }
}
