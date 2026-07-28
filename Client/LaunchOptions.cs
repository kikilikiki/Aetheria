using Aetheria.Shared;
using Aetheria.Shared.Settings;

namespace Aetheria.Client;

/// <summary>
/// Arguments passés par <c>Aetheria.Launcher</c> (voir <c>Launcher/Services/ClientLauncher.cs</c>).
/// Sans <see cref="SessionToken"/>, le Client tourne en mode démo hors-ligne (utile pour
/// développer le moteur sans lancer le Launcher/Server).
/// </summary>
public sealed record LaunchOptions(string? SessionToken, Guid? CharacterId, string Host, int Port)
{
    public static LaunchOptions Parse(string[] args)
    {
        string? token = null;
        Guid? characterId = null;
        // Voir GDD/demande utilisateur — "retire les localhost mais l'IP pour les autres qui ne
        // sont pas sur le même réseau" : reprend l'adresse configurée (GameSettings.ServerHost,
        // l'IP publique du serveur par défaut) plutôt que de retomber sur "localhost", utile
        // uniquement quand Client.exe est lancé directement sans --host (sans passer par le
        // Launcher).
        var host = GameSettings.Load().ServerHost;
        var port = GameInfo.DefaultGamePort;

        foreach (var arg in args)
        {
            var parts = arg.Split('=', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            switch (parts[0])
            {
                case "--token":
                    token = parts[1].Trim('"');
                    break;
                case "--characterId":
                    characterId = Guid.Parse(parts[1]);
                    break;
                case "--host":
                    host = parts[1];
                    break;
                case "--port":
                    port = int.Parse(parts[1]);
                    break;
            }
        }

        return new LaunchOptions(token, characterId, host, port);
    }
}
