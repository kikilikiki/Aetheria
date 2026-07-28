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
    /// : "modifie l'adresse du serveur de base pour que ça aille sur le serveur même sur un
    /// réseau distant". Réglée par défaut sur l'IP publique du serveur (redirection de ports
    /// classique côté routeur, voir Docs/README.md) plutôt que "localhost" : fonctionne aussi
    /// bien en local (le serveur écoute sur 0.0.0.0) que depuis un autre réseau, sans réglage
    /// supplémentaire. Utilisée à la fois par le Launcher (API de compte, port 7778) et transmise
    /// au Client (`--host`) pour la connexion TCP de jeu (port 7777), afin que les deux ciblent
    /// toujours le même serveur. Réglable dans les Paramètres du Launcher si ce serveur change
    /// d'adresse. Le tunnel ngrok a été retiré (voir GDD/demande utilisateur) : un seul champ à
    /// renseigner désormais, plus deux.
    /// </summary>
    public string ServerHost { get; set; } = "176.150.163.19";

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
