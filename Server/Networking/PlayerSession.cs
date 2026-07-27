using System.Net.Sockets;
using Aetheria.Database.Context;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Network;
using Aetheria.Shared.Network.Packets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aetheria.Server.Networking;

/// <summary>
/// Gère une connexion TCP de jeu unique : lit les packets du client, les traite, répond.
/// Chaque session tourne sur son propre thread (I/O bloquante volontairement simple pour
/// cette première version — voir <c>Docs/README.md</c> pour la feuille de route réseau).
/// </summary>
public sealed class PlayerSession(
    TcpClient client,
    SessionTokenStore tokenStore,
    IDbContextFactory<AetheriaDbContext> dbContextFactory,
    ILogger<PlayerSession> logger)
{
    private Guid? _characterId;

    public void Run(CancellationToken ct)
    {
        using var _ = client;
        var remoteEndPoint = client.Client.RemoteEndPoint;
        var stream = client.GetStream();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var packet = PacketFraming.ReadPacket(stream);
                if (packet is null)
                {
                    break;
                }

                HandlePacket(stream, packet);
            }
        }
        catch (IOException)
        {
            // Connexion interrompue par le client : rien de plus à faire.
        }
        finally
        {
            logger.LogInformation("Session terminée pour {Endpoint} (personnage {CharacterId}).", remoteEndPoint, _characterId);
        }
    }

    private void HandlePacket(NetworkStream stream, IPacket packet)
    {
        switch (packet)
        {
            case PingPacket ping:
                PacketFraming.WritePacket(stream, new PongPacket { TimestampUtcTicks = ping.TimestampUtcTicks });
                break;

            case EnterWorldRequestPacket enterWorld:
                HandleEnterWorld(stream, enterWorld);
                break;

            case PlayerMovePacket move:
                HandlePlayerMove(stream, move);
                break;

            default:
                logger.LogWarning("Packet {OpCode} reçu mais non géré par PlayerSession.", packet.OpCode);
                break;
        }
    }

    private void HandleEnterWorld(NetworkStream stream, EnterWorldRequestPacket request)
    {
        if (!tokenStore.TryValidate(request.SessionToken, out var userId))
        {
            PacketFraming.WritePacket(stream, new EnterWorldRejectedPacket { Reason = "Session invalide ou expirée." });
            return;
        }

        using var db = dbContextFactory.CreateDbContext();
        var character = db.Characters.FirstOrDefault(c => c.Id == request.CharacterId && c.UserId == userId);

        if (character is null)
        {
            PacketFraming.WritePacket(stream, new EnterWorldRejectedPacket { Reason = "Personnage introuvable pour ce compte." });
            return;
        }

        _characterId = character.Id;

        // Position de départ : la capitale du royaume choisi. Le placement réel dans le
        // monde persistant (royaumes/donjons) arrive avec les systèmes de jeu (Phase G).
        PacketFraming.WritePacket(stream, new EnterWorldAcceptedPacket
        {
            CharacterId = character.Id,
            PositionX = 0,
            PositionY = 0,
        });

        logger.LogInformation("{CharacterName} est entré dans le monde.", character.Name);
    }

    private void HandlePlayerMove(NetworkStream stream, PlayerMovePacket move)
    {
        if (_characterId is not { } characterId)
        {
            return;
        }

        // Pas encore de validation de portée/obstacles ni de diffusion aux autres joueurs :
        // ce sera la responsabilité du World partagé (Phase G). Pour l'instant on confirme
        // simplement le déplacement à l'émetteur.
        PacketFraming.WritePacket(stream, new PlayerPositionUpdatePacket
        {
            CharacterId = characterId,
            PositionX = move.TargetX,
            PositionY = move.TargetY,
        });
    }
}
