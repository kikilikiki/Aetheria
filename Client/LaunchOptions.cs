using Aetheria.Shared;

namespace Aetheria.Client;

/// <summary>
/// Arguments passés par <c>Aetheria.Launcher</c> (voir <c>Launcher/Services/ClientLauncher.cs</c>).
/// Sans <see cref="SessionToken"/>, le Client tourne en mode démo hors-ligne (utile pour
/// développer le moteur sans lancer le Launcher/Server).
/// </summary>
public sealed record LaunchOptions(string? SessionToken, Guid? CharacterId, string Host, int Port, string? ApiBaseUrl)
{
    /// <summary>Base URL effective de l'API de compte : <see cref="ApiBaseUrl"/> (ex. tunnel ngrok) si renseigné, sinon <c>http://{Host}:{apiPort}</c>.</summary>
    public string ResolveApiBaseUrl(int apiPort) =>
        string.IsNullOrWhiteSpace(ApiBaseUrl) ? $"http://{Host}:{apiPort}" : ApiBaseUrl.TrimEnd('/');

    public static LaunchOptions Parse(string[] args)
    {
        string? token = null;
        Guid? characterId = null;
        var host = "localhost";
        var port = GameInfo.DefaultGamePort;
        string? apiBaseUrl = null;

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
                case "--apiUrl":
                    apiBaseUrl = parts[1].Trim('"');
                    break;
            }
        }

        return new LaunchOptions(token, characterId, host, port, apiBaseUrl);
    }
}
