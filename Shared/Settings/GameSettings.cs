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
