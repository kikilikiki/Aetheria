using System.Net;
using System.Net.Sockets;
using Aetheria.Database.Context;
using Aetheria.Server.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aetheria.Server.Networking;

/// <summary>Accepte les connexions TCP entrantes et démarre une <see cref="PlayerSession"/> par client.</summary>
public sealed class TcpGameServer(
    SessionTokenStore tokenStore,
    IDbContextFactory<AetheriaDbContext> dbContextFactory,
    ILoggerFactory loggerFactory)
{
    private readonly ILogger<TcpGameServer> _logger = loggerFactory.CreateLogger<TcpGameServer>();

    /// <summary>Partagé par toutes les <see cref="PlayerSession"/> acceptées par ce serveur (voir GDD — visibilité globale des joueurs).</summary>
    private readonly WorldSessionRegistry _registry = new();

    public async Task RunAsync(int port, CancellationToken ct)
    {
        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        _logger.LogInformation("Serveur de jeu TCP en écoute sur le port {Port}.", port);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(ct);
                _logger.LogInformation("Connexion entrante : {Endpoint}", client.Client.RemoteEndPoint);

                var session = new PlayerSession(client, tokenStore, dbContextFactory, _registry, loggerFactory.CreateLogger<PlayerSession>());
                _ = Task.Run(() => session.Run(ct), ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Arrêt normal du serveur.
        }
        finally
        {
            listener.Stop();
        }
    }
}
