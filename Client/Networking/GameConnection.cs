using System.Net.Sockets;
using Aetheria.Shared.Network;
using Aetheria.Shared.Network.Packets;

namespace Aetheria.Client.Networking;

/// <summary>
/// Connexion TCP au serveur de jeu (Aetheria.Server). Envoie les packets sur le thread
/// appelant ; les reçoit sur un thread dédié et les republie via des évènements — à charge de
/// l'appelant (voir <c>Program.cs</c>) de protéger l'état partagé avec la boucle de rendu.
/// </summary>
public sealed class GameConnection : IDisposable
{
    private readonly TcpClient _client = new();
    private NetworkStream? _stream;
    private Thread? _receiveThread;
    private volatile bool _running;

    public event Action<EnterWorldAcceptedPacket>? EnterWorldAccepted;
    public event Action<EnterWorldRejectedPacket>? EnterWorldRejected;
    public event Action<PlayerPositionUpdatePacket>? PositionUpdated;
    public event Action? Disconnected;

    public void Connect(string host, int port)
    {
        _client.Connect(host, port);
        _stream = _client.GetStream();
        _running = true;

        _receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "Aetheria-GameConnection" };
        _receiveThread.Start();
    }

    public void RequestEnterWorld(string sessionToken, Guid characterId)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Appelez Connect() avant RequestEnterWorld().");
        }

        PacketFraming.WritePacket(_stream, new EnterWorldRequestPacket
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
        });
    }

    public void SendMove(int targetX, int targetY)
    {
        if (_stream is null)
        {
            return;
        }

        PacketFraming.WritePacket(_stream, new PlayerMovePacket { TargetX = targetX, TargetY = targetY });
    }

    private void ReceiveLoop()
    {
        try
        {
            while (_running)
            {
                var packet = PacketFraming.ReadPacket(_stream!);
                if (packet is null)
                {
                    break;
                }

                switch (packet)
                {
                    case EnterWorldAcceptedPacket accepted:
                        EnterWorldAccepted?.Invoke(accepted);
                        break;
                    case EnterWorldRejectedPacket rejected:
                        EnterWorldRejected?.Invoke(rejected);
                        break;
                    case PlayerPositionUpdatePacket position:
                        PositionUpdated?.Invoke(position);
                        break;
                }
            }
        }
        catch (IOException)
        {
            // Connexion perdue — signalée via Disconnected ci-dessous.
        }
        catch (SocketException)
        {
            // Idem.
        }
        finally
        {
            _running = false;
            Disconnected?.Invoke();
        }
    }

    public void Dispose()
    {
        _running = false;
        _stream?.Dispose();
        _client.Dispose();
    }
}
