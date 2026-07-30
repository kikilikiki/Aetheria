using DiscordRPC;

namespace Aetheria.Client.Services;

/// <summary>
/// Voir GDD/demande utilisateur — "ajoute custom activite discord automatique avec les gens dans
/// son groupe si il est en combat en donjon etage combien etc" : Rich Presence Discord via IPC
/// local (le client Discord de bureau doit tourner sur la machine — se connecte en arrière-plan et
/// réessaie tout seul sinon, aucune erreur ne remonte au joueur, voir DiscordRpcClient). N'envoie
/// une mise à jour à Discord que lorsque le texte affiché change réellement (évite de spammer
/// l'IPC à chaque frame pour rien).
/// </summary>
public sealed class DiscordPresenceService : IDisposable
{
    // Voir demande utilisateur — identifiant de l'application Discord fourni pour cette activité.
    private const string ApplicationId = "1531274709222559814";

    private readonly DiscordRpcClient _client;
    private readonly DateTime _startedAtUtc = DateTime.UtcNow;
    private string? _lastDetails;
    private string? _lastState;

    public DiscordPresenceService()
    {
        _client = new DiscordRpcClient(ApplicationId);
        _client.Initialize();
    }

    /// <summary>À appeler à chaque frame — traite les évènements internes de la connexion IPC (voir doc DiscordRPC), sans effet si Discord n'est pas lancé.</summary>
    public void Invoke() => _client.Invoke();

    /// <summary><paramref name="details"/> est la ligne principale (ex. "En donjon — Étage 3"), <paramref name="state"/> la ligne secondaire (ex. "Avec : Alice, Bob") — null si non applicable.</summary>
    public void Update(string details, string? state)
    {
        if (details == _lastDetails && state == _lastState)
        {
            return;
        }

        _lastDetails = details;
        _lastState = state;

        _client.SetPresence(new RichPresence
        {
            Details = details,
            State = state,
            Timestamps = new Timestamps { Start = _startedAtUtc },
        });
    }

    public void Dispose() => _client.Dispose();
}
