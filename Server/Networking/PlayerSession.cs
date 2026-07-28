using System.Net.Sockets;
using Aetheria.Database.Context;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Network;
using Aetheria.Shared.Network.Packets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aetheria.Server.Networking;

/// <summary>
/// Gère une connexion TCP de jeu unique : lit les packets du client, les traite, répond.
/// Chaque session tourne sur son propre thread (I/O bloquante volontairement simple pour
/// cette première version — voir <c>Docs/README.md</c> pour la feuille de route réseau).
/// Enregistrée dans <see cref="WorldSessionRegistry"/> une fois entrée dans le monde, pour que
/// les autres joueurs la voient bouger en temps réel (voir GDD — visibilité globale).
/// </summary>
public sealed class PlayerSession(
    TcpClient client,
    SessionTokenStore tokenStore,
    IDbContextFactory<AetheriaDbContext> dbContextFactory,
    WorldSessionRegistry registry,
    ILogger<PlayerSession> logger)
{
    private readonly object _writeLock = new();
    private NetworkStream? _stream;

    public Guid CharacterId { get; private set; }
    public string CharacterName { get; private set; } = string.Empty;
    public Guid UserId { get; private set; }
    public UserRank Rank { get; private set; } = UserRank.Joueur;
    public int PositionX { get; private set; }
    public int PositionY { get; private set; }

    private bool HasEnteredWorld => CharacterId != Guid.Empty;

    public void Run(CancellationToken ct)
    {
        using var _ = client;
        var remoteEndPoint = client.Client.RemoteEndPoint;
        _stream = client.GetStream();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var packet = PacketFraming.ReadPacket(_stream);
                if (packet is null)
                {
                    break;
                }

                HandlePacket(packet);
            }
        }
        catch (IOException)
        {
            // Connexion interrompue par le client : rien de plus à faire.
        }
        finally
        {
            if (HasEnteredWorld)
            {
                registry.Unregister(CharacterId);
                registry.BroadcastExcept(CharacterId, new PlayerLeftPacket { CharacterId = CharacterId });
            }

            logger.LogInformation("Session terminée pour {Endpoint} (personnage {CharacterId}).", remoteEndPoint, CharacterId);
        }
    }

    /// <summary>Envoi thread-safe : appelé aussi bien par le thread de cette session que par ceux d'autres sessions (diffusion via <see cref="WorldSessionRegistry"/>).</summary>
    public void SendPacket(IPacket packet)
    {
        if (_stream is null)
        {
            return;
        }

        lock (_writeLock)
        {
            try
            {
                PacketFraming.WritePacket(_stream, packet);
            }
            catch (IOException)
            {
                // La session en cours de fermeture ignore les diffusions tardives.
            }
        }
    }

    private void HandlePacket(IPacket packet)
    {
        switch (packet)
        {
            case PingPacket ping:
                SendPacket(new PongPacket { TimestampUtcTicks = ping.TimestampUtcTicks });
                break;

            case EnterWorldRequestPacket enterWorld:
                HandleEnterWorld(enterWorld);
                break;

            case PlayerMovePacket move:
                HandlePlayerMove(move);
                break;

            case ChatMessagePacket chat:
                HandleChatMessage(chat);
                break;

            default:
                logger.LogWarning("Packet {OpCode} reçu mais non géré par PlayerSession.", packet.OpCode);
                break;
        }
    }

    private void HandleEnterWorld(EnterWorldRequestPacket request)
    {
        if (!tokenStore.TryValidate(request.SessionToken, out var userId))
        {
            SendPacket(new EnterWorldRejectedPacket { Reason = "Session invalide ou expirée." });
            return;
        }

        using var db = dbContextFactory.CreateDbContext();
        var character = db.Characters.FirstOrDefault(c => c.Id == request.CharacterId && c.UserId == userId);

        if (character is null)
        {
            SendPacket(new EnterWorldRejectedPacket { Reason = "Personnage introuvable pour ce compte." });
            return;
        }

        CharacterId = character.Id;
        CharacterName = character.Name;
        UserId = userId;
        Rank = db.Users.Where(u => u.Id == userId).Select(u => u.Rank).FirstOrDefault();

        // Position de départ : la capitale du royaume choisi. Le placement réel dans le
        // monde persistant (royaumes/donjons) arrive avec les systèmes de jeu (Phase G).
        PositionX = 0;
        PositionY = 0;

        SendPacket(new EnterWorldAcceptedPacket
        {
            CharacterId = character.Id,
            PositionX = PositionX,
            PositionY = PositionY,
            Rank = Rank,
        });

        // Snapshot des joueurs déjà connectés (voir GDD — visibilité globale) : une série de
        // PlayerJoined plutôt qu'un packet "snapshot" séparé, pris AVANT de s'enregistrer
        // soi-même pour ne pas se recevoir soi-même dans la liste.
        foreach (var other in registry.All())
        {
            SendPacket(new PlayerJoinedPacket
            {
                CharacterId = other.CharacterId,
                Name = other.CharacterName,
                PositionX = other.PositionX,
                PositionY = other.PositionY,
                Rank = other.Rank,
            });
        }

        registry.Register(this);
        registry.BroadcastExcept(CharacterId, new PlayerJoinedPacket
        {
            CharacterId = CharacterId,
            Name = CharacterName,
            PositionX = PositionX,
            PositionY = PositionY,
            Rank = Rank,
        });

        logger.LogInformation("{CharacterName} est entré dans le monde.", character.Name);
    }

    private void HandlePlayerMove(PlayerMovePacket move)
    {
        if (!HasEnteredWorld)
        {
            return;
        }

        // Pas encore de validation de portée/obstacles côté serveur (voir GDD — Phase G pour un
        // monde partagé complet) : la position envoyée par le client est acceptée telle quelle,
        // puis diffusée à tous — y compris à l'émetteur, pour rester la seule source de vérité
        // sur sa propre position affichée (comportement autoritaire déjà en place côté Client).
        PositionX = move.TargetX;
        PositionY = move.TargetY;

        var update = new PlayerPositionUpdatePacket { CharacterId = CharacterId, PositionX = PositionX, PositionY = PositionY };
        SendPacket(update);
        registry.BroadcastExcept(CharacterId, update);
    }

    /// <summary>
    /// Tchat global (tout le monde) ou tchat de guilde (voir GDD/demande utilisateur). Le serveur
    /// ignore le nom/grade envoyés par le client (usurpation impossible) et renseigne les siens,
    /// mis en cache sur la session depuis <see cref="HandleEnterWorld"/>.
    /// </summary>
    private void HandleChatMessage(ChatMessagePacket chat)
    {
        if (!HasEnteredWorld)
        {
            return;
        }

        var trimmed = chat.Message.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        var outgoing = new ChatMessagePacket
        {
            SenderName = CharacterName,
            Message = trimmed,
            Channel = chat.Channel,
            Rank = Rank,
        };

        if (chat.Channel == ChatChannel.Guild)
        {
            using var db = dbContextFactory.CreateDbContext();
            var guildId = db.GuildMembers
                .Where(m => m.CharacterId == CharacterId)
                .Select(m => (Guid?)m.GuildId)
                .FirstOrDefault();

            if (guildId is null)
            {
                SendPacket(new ChatMessagePacket
                {
                    SenderName = "Système",
                    Message = "Vous n'êtes dans aucune guilde.",
                    Channel = ChatChannel.Guild,
                });
                return;
            }

            var guildMemberIds = db.GuildMembers
                .Where(m => m.GuildId == guildId)
                .Select(m => m.CharacterId)
                .ToHashSet();

            foreach (var session in registry.All().Where(s => guildMemberIds.Contains(s.CharacterId)))
            {
                session.SendPacket(outgoing);
            }
        }
        else
        {
            foreach (var session in registry.All())
            {
                session.SendPacket(outgoing);
            }
        }
    }
}
