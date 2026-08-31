using System.Collections.Concurrent;
using Aetheria.Shared.Enums;
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

    /// <summary>
    /// Voir demande utilisateur — "message d'annonce entre les modos pour toutes les actions" :
    /// n'envoie qu'aux sessions du staff (permission technique <c>IsAdmin</c> ou grade
    /// communautaire Modérateur/Fondateur, tous deux renseignés au login — voir PlayerSession).
    /// </summary>
    public void SendToStaff(IPacket packet)
    {
        foreach (var session in All().Where(IsStaff))
        {
            session.SendPacket(packet);
        }
    }

    /// <summary>Le staff + un joueur précis (voir demande utilisateur — le destinataire d'un monstre voit le message, comme les modos). Pas d'envoi en double si le destinataire est lui-même staff.</summary>
    public void SendToCharacterAndStaff(Guid characterId, IPacket packet)
    {
        SendToStaff(packet);

        var target = _sessions.GetValueOrDefault(characterId);
        if (target is not null && !IsStaff(target))
        {
            target.SendPacket(packet);
        }
    }

    private static bool IsStaff(PlayerSession session) =>
        session.IsAdmin || session.Rank is UserRank.Moderateur or UserRank.Fondateur;

    public PlayerSession? FindByCharacterId(Guid characterId) => _sessions.GetValueOrDefault(characterId);

    public PlayerSession? FindByCharacterName(string characterName) =>
        _sessions.Values.FirstOrDefault(s => s.CharacterName.Equals(characterName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Voir GDD/demande utilisateur — liste d'amis "en ligne/hors ligne".</summary>
    public bool IsOnline(Guid characterId) => _sessions.ContainsKey(characterId);
}
