using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aetheria.Database.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aetheria.Server.Discord;

/// <summary>
/// Connexion Discord Gateway (WebSocket) permettant de recevoir la commande slash <c>/link</c>
/// (voir GDD/demande utilisateur — "système de link le compte discord avec le jeu... commandes
/// de link"). Contrairement à <see cref="DiscordAnnouncer"/> (envoi sortant uniquement, REST),
/// recevoir une commande nécessite soit un endpoint HTTPS public (nécessite un nom de domaine +
/// certificat valide, absent ici — voir Sites/README.md, seul un tunnel ngrok optionnel existe et
/// change d'URL à chaque relance), soit une connexion Gateway sortante classique — choisie ici
/// car elle ne nécessite aucune exposition réseau entrante, donc fonctionne à l'identique pour
/// l'instance prod et l'instance dev (voir demande utilisateur "bot actif avec le serveur (prod
/// et dev)") : chacune ouvre sa propre connexion avec son propre <c>DISCORD_BOT_TOKEN</c>/
/// <c>DISCORD_GUILD_IDS</c> (voir .env), sans dépendre l'une de l'autre.
///
/// Implémentation volontairement minimale (pas de librairie Discord.Net/DSharpPlus, même choix
/// que <see cref="DiscordAnnouncer"/>) : identify + heartbeat + dispatch des interactions
/// seulement. Pas de vrai support du RESUME (op 6) — une reconnexion relance une session neuve
/// (ré-IDENTIFY), suffisant pour ce besoin (le seul état à ne pas perdre, les codes de lien en
/// attente, vit en base de données, pas en mémoire de session Gateway).
/// </summary>
public sealed class DiscordGatewayClient(
    IDbContextFactory<AetheriaDbContext> dbFactory,
    DiscordRoleSyncService roleSyncService,
    ILogger<DiscordGatewayClient> logger) : BackgroundService
{
    private const string GatewayUrl = "wss://gateway.discord.gg/?v=10&encoding=json";
    private const string ApiBaseUrl = "https://discord.com/api/v10/";

    private readonly HttpClient _http = new() { BaseAddress = new Uri(ApiBaseUrl) };
    private string? _botToken;
    private string? _applicationId;
    private List<string> _guildIds = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _botToken = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
        _applicationId = Environment.GetEnvironmentVariable("DISCORD_APPLICATION_ID");
        var guildsEnv = Environment.GetEnvironmentVariable("DISCORD_GUILD_IDS");
        _guildIds = guildsEnv is { Length: > 0 }
            ? guildsEnv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : [];

        if (_botToken is not { Length: > 0 } || _applicationId is not { Length: > 0 } || _guildIds.Count == 0)
        {
            logger.LogInformation("DISCORD_BOT_TOKEN/DISCORD_APPLICATION_ID/DISCORD_GUILD_IDS absent(s) : commande Discord /link désactivée.");
            return;
        }

        await RegisterSlashCommandAsync(stoppingToken);

        // Boucle de reconnexion : toute déconnexion (perte réseau, redémarrage côté Discord,
        // session invalide) relance simplement une session neuve après une courte pause.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSessionAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Session Discord Gateway interrompue, reconnexion dans 10s.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RegisterSlashCommandAsync(CancellationToken ct)
    {
        var command = new
        {
            name = "link",
            description = "Lie ton compte Discord à ton compte Aetheria",
            options = new[]
            {
                new
                {
                    type = 3, // STRING
                    name = "code",
                    description = "Code affiché en jeu (commande /discord)",
                    required = true,
                },
            },
        };

        foreach (var guildId in _guildIds)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Put, $"applications/{_applicationId}/guilds/{guildId}/commands")
                {
                    Content = JsonContent.Create(new[] { command }),
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bot", _botToken);

                var response = await _http.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    logger.LogWarning("Échec d'enregistrement de la commande /link sur la guilde {GuildId} : {Status} {Body}", guildId, response.StatusCode, body);
                }
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Impossible de contacter Discord pour enregistrer la commande /link sur la guilde {GuildId}.", guildId);
            }
        }
    }

    private async Task RunSessionAsync(CancellationToken ct)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(GatewayUrl), ct);

        var buffer = new byte[16 * 1024];
        var hello = await ReceiveJsonAsync(socket, buffer, ct) ?? throw new IOException("Gateway fermée avant HELLO.");
        var heartbeatIntervalMs = hello["d"]!["heartbeat_interval"]!.GetValue<int>();

        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var lastSequence = (int?)null;
        var heartbeatTask = RunHeartbeatLoopAsync(socket, heartbeatIntervalMs, () => lastSequence, heartbeatCts.Token);

        try
        {
            await SendJsonAsync(socket, new JsonObject
            {
                ["op"] = 2,
                ["d"] = new JsonObject
                {
                    ["token"] = _botToken,
                    ["intents"] = 0,
                    ["properties"] = new JsonObject { ["os"] = "windows", ["browser"] = "aetheria", ["device"] = "aetheria" },
                },
            }, ct);

            while (!ct.IsCancellationRequested)
            {
                var payload = await ReceiveJsonAsync(socket, buffer, ct);
                if (payload is null)
                {
                    break;
                }

                var op = payload["op"]!.GetValue<int>();
                if (payload["s"]?.GetValue<int?>() is { } sequence)
                {
                    lastSequence = sequence;
                }

                switch (op)
                {
                    case 0: // Dispatch
                        await HandleDispatchAsync(payload, ct);
                        break;
                    case 7: // Reconnect requested by Discord
                    case 9: // Invalid session
                        return;
                }
            }
        }
        finally
        {
            heartbeatCts.Cancel();
            try
            {
                await heartbeatTask;
            }
            catch (OperationCanceledException)
            {
            }

            if (socket.State == WebSocketState.Open)
            {
                try
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                }
                catch (Exception)
                {
                    // Fermeture best-effort — la session va de toute façon être recréée.
                }
            }
        }
    }

    private static async Task RunHeartbeatLoopAsync(ClientWebSocket socket, int intervalMs, Func<int?> getSequence, CancellationToken ct)
    {
        // Premier battement après un délai aléatoire (jitter) conforme à la documentation Discord,
        // pour éviter que toutes les connexions envoient leur premier heartbeat au même instant.
        var jitterMs = (int)(intervalMs * Random.Shared.NextDouble());
        await Task.Delay(jitterMs, ct);

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMs));
        do
        {
            await SendJsonAsync(socket, new JsonObject { ["op"] = 1, ["d"] = getSequence() }, ct);
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    private async Task HandleDispatchAsync(JsonNode payload, CancellationToken ct)
    {
        var eventType = payload["t"]?.GetValue<string>();
        if (eventType != "INTERACTION_CREATE")
        {
            return;
        }

        var interaction = payload["d"]!;
        if (interaction["type"]!.GetValue<int>() != 2) // APPLICATION_COMMAND
        {
            return;
        }

        var commandName = interaction["data"]?["name"]?.GetValue<string>();
        if (commandName != "link")
        {
            return;
        }

        var interactionId = interaction["id"]!.GetValue<string>();
        var interactionToken = interaction["token"]!.GetValue<string>();
        var discordUserId = interaction["member"]?["user"]?["id"]?.GetValue<string>()
            ?? interaction["user"]?["id"]?.GetValue<string>();
        var code = interaction["data"]?["options"]?.AsArray()
            .FirstOrDefault(o => o?["name"]?.GetValue<string>() == "code")?["value"]?.GetValue<string>();

        if (discordUserId is null || code is null)
        {
            await RespondAsync(interactionId, interactionToken, "Commande invalide.", ct);
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var (result, user) = await DiscordLinkService.TryLinkAsync(db, code, discordUserId, ct);

        // Voir demande utilisateur — "un utilisateur ne peut se vérifier plus de 1 fois" : message
        // distinct pour chaque cas plutôt qu'un "code invalide" générique trompeur.
        var responseMessage = result switch
        {
            DiscordLinkService.LinkResult.Success => $"Compte lié avec succès à **{user!.Username}** ! Ton rôle a été attribué.",
            DiscordLinkService.LinkResult.AccountAlreadyLinked => "Ce compte Aetheria est déjà vérifié — impossible de le lier une seconde fois.",
            DiscordLinkService.LinkResult.DiscordAccountAlreadyLinked => "Ton compte Discord est déjà lié à un autre compte Aetheria.",
            _ => "Code invalide ou expiré. Tape /discord en jeu pour en générer un nouveau.",
        };

        if (result == DiscordLinkService.LinkResult.Success && user is not null)
        {
            await roleSyncService.SyncUserRoleAsync(user, ct);
        }

        await RespondAsync(interactionId, interactionToken, responseMessage, ct);
    }

    private async Task RespondAsync(string interactionId, string interactionToken, string message, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"interactions/{interactionId}/{interactionToken}/callback")
            {
                Content = JsonContent.Create(new
                {
                    type = 4, // CHANNEL_MESSAGE_WITH_SOURCE
                    data = new { content = message, flags = 64 }, // 64 = EPHEMERAL, visible seulement par l'auteur de la commande
                }),
            };

            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Échec de réponse à l'interaction Discord /link : {Status} {Body}", response.StatusCode, body);
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Impossible de répondre à l'interaction Discord /link.");
        }
    }

    private static async Task SendJsonAsync(ClientWebSocket socket, JsonNode payload, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(payload.ToJsonString());
        await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
    }

    private static async Task<JsonNode?> ReceiveJsonAsync(ClientWebSocket socket, byte[] buffer, CancellationToken ct)
    {
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            stream.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        stream.Position = 0;
        return JsonNode.Parse(stream);
    }
}
