using System.Collections.Concurrent;
using Aetheria.Shared.Network;

namespace Aetheria.Server.Networking;

/// <summary>
/// Sessions de joueurs actuellement connectés au monde partagé (voir <c>Docs/GameDesign.md</c> —
/// "on peut voir tout le monde en jeu même quand on n'est pas en groupe"). Registre en mémoire
/// partagé entre tous les threads <see cref="PlayerSession"/> (un thread bloquant par connexion
/// TCP, voir <c>TcpGameServer</c>) — <see cref="ConcurrentDictionary{TKey,TValue}"/> suffit ici,
/// l'ordre d'écriture sur chaque connexion individuelle étant protégé côté
/// <see cref="PlayerSession.SendPacket"/>.
/// </summary>
public sealed class WorldSessionRegistry
{
    private readonly ConcurrentDictionary<Guid, PlayerSession> _sessions = new();

    public void Register(PlayerSession session) => _sessions[session.CharacterId] = session;

    public void Unregister(Guid characterId) => _sessions.TryRemove(characterId, out _);

    public IReadOnlyCollection<PlayerSession> AllExcept(Guid characterId) =>
        _sessions.Values.Where(s => s.CharacterId != characterId).ToList();

    public IReadOnlyCollection<PlayerSession> All() => _sessions.Values.ToList();

    public void BroadcastExcept(Guid characterId, IPacket packet)
    {
        foreach (var session in AllExcept(characterId))
        {
            session.SendPacket(packet);
        }
    }

    /// <summary>Voir GDD/demande utilisateur — panel admin en jeu (message à tous, transformation de skin) : personne à exclure ici, contrairement à <see cref="BroadcastExcept"/>.</summary>
    public void BroadcastAll(IPacket packet)
    {
        foreach (var session in All())
        {
            session.SendPacket(packet);
        }
    }

    public PlayerSession? FindByCharacterName(string characterName) =>
        _sessions.Values.FirstOrDefault(s => s.CharacterName.Equals(characterName, StringComparison.OrdinalIgnoreCase));
}
