using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Shared.Enums;
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
    BetaTicketService betaTickets,
    ILogger<DiscordGatewayClient> logger) : BackgroundService
{
    private const string GatewayUrl = "wss://gateway.discord.gg/?v=10&encoding=json";
    private const string ApiBaseUrl = "https://discord.com/api/v10/";

    private readonly HttpClient _http = new() { BaseAddress = new Uri(ApiBaseUrl) };
    private string? _botToken;
    private string? _applicationId;
    private List<string> _guildIds = [];

    /// <summary>
    /// Passe à <c>false</c> à chaque envoi de heartbeat, revient à <c>true</c> quand Discord ACK
    /// (op 11). Si un heartbeat part alors que le précédent n'a jamais été ACK, la connexion est
    /// « zombie » (à moitié morte) : on la coupe et on reconnecte, plutôt que de rester bloqué
    /// indéfiniment en réception sans jamais recevoir d'interaction (cause du bug « les boutons du
    /// ticket ne répondent plus après un moment / un redémarrage »).
    /// </summary>
    private volatile bool _heartbeatAcked = true;

    /// <summary>Reçu à op 11, sinon la connexion est considérée morte.</summary>
    private DateTime _lastHeartbeatAckUtc = DateTime.UtcNow;

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
            logger.LogWarning("DISCORD_BOT_TOKEN/DISCORD_APPLICATION_ID/DISCORD_GUILD_IDS absent(s) : Gateway Discord (commande /link + boutons de ticket bêta) DÉSACTIVÉE.");
            return;
        }

        logger.LogInformation("Gateway Discord : démarrage (application {AppId}, guildes {Guilds}).", _applicationId, string.Join(",", _guildIds));
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
        var linkCommand = new
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

        // Voir demande utilisateur — "ajoute une commande de unlink" : aucun code nécessaire,
        // l'identité Discord de l'auteur de la commande suffit à retrouver le compte à délier.
        var unlinkCommand = new
        {
            name = "unlink",
            description = "Délie ton compte Discord de ton compte Aetheria",
        };

        foreach (var guildId in _guildIds)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Put, $"applications/{_applicationId}/guilds/{guildId}/commands")
                {
                    Content = JsonContent.Create(new object[] { linkCommand, unlinkCommand }),
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bot", _botToken);

                var response = await _http.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    logger.LogWarning("Échec d'enregistrement des commandes /link et /unlink sur la guilde {GuildId} : {Status} {Body}", guildId, response.StatusCode, body);
                }
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Impossible de contacter Discord pour enregistrer les commandes /link et /unlink sur la guilde {GuildId}.", guildId);
            }
        }
    }

    private async Task RunSessionAsync(CancellationToken ct)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(GatewayUrl), ct);
        logger.LogInformation("Gateway Discord : WebSocket connecté, envoi de l'IDENTIFY.");

        var buffer = new byte[64 * 1024];
        var hello = await ReceiveJsonAsync(socket, buffer, ct) ?? throw new IOException("Gateway fermée avant HELLO.");
        var heartbeatIntervalMs = hello["d"]!["heartbeat_interval"]!.GetValue<int>();

        _heartbeatAcked = true;
        _lastHeartbeatAckUtc = DateTime.UtcNow;

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
                    // Aucun intent : les interactions (boutons de ticket, commandes /link) sont
                    // toujours livrées, indépendamment des intents. Le message de bienvenue part
                    // désormais à l'acceptation d'une candidature (BetaTicketService.PostWelcomeAsync),
                    // plus à l'arrivée sur le serveur Discord — GUILD_MEMBERS n'est plus nécessaire.
                    ["intents"] = 0,
                    ["properties"] = new JsonObject { ["os"] = "windows", ["browser"] = "aetheria", ["device"] = "aetheria" },
                },
            }, ct);

            while (!ct.IsCancellationRequested)
            {
                var payload = await ReceiveJsonAsync(socket, buffer, ct);
                if (payload is null)
                {
                    logger.LogInformation("Gateway Discord : connexion fermée par Discord ({Status} {Desc}), reconnexion.", socket.CloseStatus, socket.CloseStatusDescription);
                    return;
                }

                var op = payload["op"]!.GetValue<int>();
                if (payload["s"]?.GetValue<int?>() is { } sequence)
                {
                    lastSequence = sequence;
                }

                switch (op)
                {
                    case 0: // Dispatch
                        if (payload["t"]?.GetValue<string>() == "READY")
                        {
                            logger.LogInformation("Gateway Discord : READY — prêt à recevoir les interactions (boutons de ticket bêta inclus).");
                        }

                        // Traité en tâche de fond : une requête base/Discord lente (ex. clic sur un
                        // bouton de ticket) ne doit pas bloquer la boucle de réception ni la
                        // détection de connexion morte.
                        var dispatchPayload = payload;
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await HandleDispatchAsync(dispatchPayload, ct);
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex, "Gateway Discord : erreur en traitant un évènement (ignorée).");
                            }
                        }, ct);
                        break;
                    case 1: // Discord demande un heartbeat immédiat
                        _heartbeatAcked = false;
                        await SendJsonAsync(socket, new JsonObject { ["op"] = 1, ["d"] = lastSequence }, ct);
                        break;
                    case 11: // Heartbeat ACK
                        _heartbeatAcked = true;
                        _lastHeartbeatAckUtc = DateTime.UtcNow;
                        break;
                    case 7: // Reconnect requested by Discord
                    case 9: // Invalid session
                        logger.LogInformation("Gateway Discord : op {Op} reçu, reconnexion.", op);
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

    private async Task RunHeartbeatLoopAsync(ClientWebSocket socket, int intervalMs, Func<int?> getSequence, CancellationToken ct)
    {
        // Premier battement après un délai aléatoire (jitter) conforme à la documentation Discord,
        // pour éviter que toutes les connexions envoient leur premier heartbeat au même instant.
        var jitterMs = (int)(intervalMs * Random.Shared.NextDouble());
        await Task.Delay(jitterMs, ct);

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMs));
        do
        {
            // Discord n'a pas ACK le heartbeat précédent : connexion morte (voir doc Discord —
            // « close the connection with a non-1000 close code, reconnect »). On coupe le socket,
            // ce qui débloque ReceiveJsonAsync et déclenche la reconnexion.
            if (!_heartbeatAcked)
            {
                logger.LogWarning("Gateway Discord : aucun ACK de heartbeat depuis {Age:g} — connexion morte, on la coupe et on reconnecte.", DateTime.UtcNow - _lastHeartbeatAckUtc);
                try
                {
                    await socket.CloseOutputAsync(WebSocketCloseStatus.EndpointUnavailable, "no heartbeat ack", CancellationToken.None);
                }
                catch (Exception)
                {
                    // best-effort
                }

                socket.Abort();
                return;
            }

            _heartbeatAcked = false;
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
        var interactionType = interaction["type"]?.GetValue<int>() ?? 0;
        var interactionId = interaction["id"]!.GetValue<string>();
        var interactionToken = interaction["token"]!.GetValue<string>();
        var discordUserId = interaction["member"]?["user"]?["id"]?.GetValue<string>()
            ?? interaction["user"]?["id"]?.GetValue<string>();

        logger.LogInformation("Interaction Discord reçue : type={Type}, user={User}, customId={CustomId}",
            interactionType, discordUserId, interaction["data"]?["custom_id"]?.GetValue<string>());

        if (discordUserId is null)
        {
            return;
        }

        // Boutons d'un ticket bêta (voir BetaTicketService) : Accepter / Refuser / Fermer le ticket.
        if (interactionType == 3) // MESSAGE_COMPONENT
        {
            var customId = interaction["data"]?["custom_id"]?.GetValue<string>();
            if (customId == "beta_close")
            {
                await HandleBetaCloseAsync(interaction, interactionId, interactionToken, discordUserId, ct);
            }
            else if (customId is not null && (customId.StartsWith("beta_accept:") || customId.StartsWith("beta_reject:")))
            {
                await HandleBetaDecisionAsync(interaction, interactionId, interactionToken, discordUserId, ct);
            }

            return;
        }

        if (interactionType != 2) // APPLICATION_COMMAND
        {
            return;
        }

        var commandName = interaction["data"]?["name"]?.GetValue<string>();

        switch (commandName)
        {
            case "link":
                await HandleLinkCommandAsync(interaction, interactionId, interactionToken, discordUserId, ct);
                break;
            case "unlink":
                await HandleUnlinkCommandAsync(interactionId, interactionToken, discordUserId, ct);
                break;
        }
    }

    /// <summary>
    /// Vrai si l'auteur de l'interaction a le droit de décider / fermer un ticket bêta : un rôle
    /// de <see cref="BetaTicketService.DecisionRoleIds"/> ou <see cref="BetaTicketService.StaffRoleIds"/>,
    /// ou la permission Administrateur sur le serveur.
    /// </summary>
    private bool IsAuthorizedBetaStaff(JsonNode interaction, string discordUserId, string context, out string reviewer)
    {
        reviewer = interaction["member"]?["nick"]?.GetValue<string>()
            ?? interaction["member"]?["user"]?["global_name"]?.GetValue<string>()
            ?? interaction["member"]?["user"]?["username"]?.GetValue<string>()
            ?? "le staff";

        var memberRoles = (interaction["member"]?["roles"]?.AsArray() ?? [])
            .Select(r => r?.GetValue<string>())
            .Where(r => r is not null)
            .ToHashSet();

        var hasAdminPerms = ulong.TryParse(interaction["member"]?["permissions"]?.GetValue<string>(), out var perms)
            && (perms & 0x8UL) != 0; // ADMINISTRATOR

        var allowedRoles = betaTickets.DecisionRoleIds.Concat(betaTickets.StaffRoleIds).ToHashSet();
        var authorized = hasAdminPerms || allowedRoles.Any(memberRoles.Contains);

        logger.LogInformation(
            "Bouton bêta ({Context}) : user={User}, rolesMembre=[{Roles}], rolesAutorises=[{Allowed}], admin={Admin} -> {Result}",
            context, discordUserId, string.Join(",", memberRoles), string.Join(",", allowedRoles), hasAdminPerms,
            authorized ? "autorisé" : "refusé");

        return authorized;
    }

    private string AllowedRolesMessage() =>
        "⛔ Réservé au staff. Rôle(s) autorisé(s) : "
        + string.Join(", ", betaTickets.DecisionRoleIds.Concat(betaTickets.StaffRoleIds).Distinct().Select(r => $"<@&{r}>"))
        + ".";

    private async Task HandleBetaCloseAsync(JsonNode interaction, string interactionId, string interactionToken, string discordUserId, CancellationToken ct)
    {
        if (!IsAuthorizedBetaStaff(interaction, discordUserId, "fermer", out _))
        {
            await RespondAsync(interactionId, interactionToken, AllowedRolesMessage(), ct);
            return;
        }

        var channelId = interaction["channel_id"]?.GetValue<string>()
            ?? interaction["channel"]?["id"]?.GetValue<string>();
        if (channelId is null)
        {
            return;
        }

        // Répondre AVANT de supprimer (le salon disparaît avec l'interaction).
        await RespondAsync(interactionId, interactionToken, "🔒 Fermeture du ticket…", ct);
        var ok = await betaTickets.DeleteChannelAsync(channelId, ct);
        logger.LogInformation("Ticket bêta {ChannelId} fermé par {User} : {Result}.", channelId, discordUserId, ok ? "ok" : "échec");
    }

    private async Task HandleBetaDecisionAsync(JsonNode interaction, string interactionId, string interactionToken, string discordUserId, CancellationToken ct)
    {
        var customId = interaction["data"]?["custom_id"]?.GetValue<string>()!;
        var accept = customId.StartsWith("beta_accept:");
        if (!Guid.TryParse(customId.Split(':', 2)[1], out var applicationId))
        {
            return;
        }

        if (!IsAuthorizedBetaStaff(interaction, discordUserId, accept ? "accepter" : "refuser", out var reviewer))
        {
            await RespondAsync(interactionId, interactionToken, AllowedRolesMessage(), ct);
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var application = await db.BetaApplications.FirstOrDefaultAsync(a => a.Id == applicationId, ct);
        if (application is null)
        {
            await RespondAsync(interactionId, interactionToken, "Candidature introuvable.", ct);
            return;
        }

        if (application.Status != BetaApplicationStatus.Pending)
        {
            await RespondAsync(interactionId, interactionToken, $"Cette candidature a déjà été traitée ({application.Status}).", ct);
            return;
        }

        application.Status = accept ? BetaApplicationStatus.Approved : BetaApplicationStatus.Rejected;
        application.ReviewedByUsername = reviewer;
        application.ReviewedAtUtc = DateTime.UtcNow;
        application.SyncedStatus = application.Status; // décision déjà répercutée ici, le processor ne repostera pas
        if (!accept)
        {
            application.AdminNote ??= "Refusée depuis le ticket Discord.";
        }

        string? newReferralCode = null;
        string? newReferralUsername = null;
        Guid newReferralUserId = default;
        UserEntity? approvedUser = null;

        if (accept)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == application.UserId, ct);
            approvedUser = user;
            if (user is not null)
            {
                if (user.Rank == UserRank.Joueur)
                {
                    user.Rank = UserRank.Testeur; // débloque le téléchargement du jeu sur le site
                }

                var hadCode = !string.IsNullOrEmpty(user.ReferralCode);
                await Aetheria.Database.Services.ReferralService.EnsureCodeAsync(db, user, ct);
                if (!hadCode && !string.IsNullOrEmpty(user.ReferralCode))
                {
                    newReferralCode = user.ReferralCode;
                    newReferralUsername = user.Username;
                    newReferralUserId = user.Id;
                }
            }

            await Aetheria.Database.Services.ReferralService.ApplyOnApprovalAsync(db, application, ct);
        }

        await db.SaveChangesAsync(ct);

        if (newReferralCode is not null)
        {
            DiscordEventLog.LogReferral(newReferralUsername!, newReferralUserId, newReferralCode);
        }

        var applicantMention = application.ResolvedDiscordUserId is { Length: > 0 } id ? $"<@{id}> " : "";
        var publicMessage = accept
            ? $"✅ Candidature **acceptée** par {reviewer}. {applicantMention}a maintenant le rôle Testeur et accès au téléchargement du jeu."
            : $"❌ Candidature **refusée** par {reviewer}.";

        await RespondAsync(interactionId, interactionToken, publicMessage, ct, ephemeral: false);

        // Best-effort après la réponse : rôle Discord + désactivation des boutons + proposition de fermeture.
        if (accept && application.ResolvedDiscordUserId is { Length: > 0 } discordId)
        {
            await betaTickets.GrantTesterRoleAsync(discordId, ct);

            // Si le compte est aussi lié via /discord + /link, synchronise proprement tous ses
            // rôles de grade (gère l'exclusivité des rôles de grade supérieurs).
            if (approvedUser?.DiscordUserId is { Length: > 0 })
            {
                await roleSyncService.SyncUserRoleAsync(approvedUser, ct);
            }

            // Voir demande utilisateur — annonce publique de bienvenue à l'acceptation.
            await betaTickets.PostWelcomeAsync(discordId, ct);
        }

        if (application.DiscordTicketChannelId is { Length: > 0 } channelId)
        {
            if (application.DiscordTicketMessageId is { Length: > 0 } messageId)
            {
                await betaTickets.DisableTicketButtonsAsync(channelId, messageId, application.Id, ct);
            }

            await betaTickets.PostCloseProposalAsync(channelId, ct);
        }

        if (accept)
        {
            // Voir demande utilisateur — récap dans le salon des acceptés (mêmes infos, sans boutons).
            await betaTickets.PostAcceptedApplicationAsync(application, reviewer, ct);
        }

        logger.LogInformation("Candidature {Id} {Decision} depuis le ticket Discord par {Reviewer}.",
            application.Id, accept ? "acceptée" : "refusée", reviewer);
    }

    private async Task HandleLinkCommandAsync(JsonNode interaction, string interactionId, string interactionToken, string discordUserId, CancellationToken ct)
    {
        var code = interaction["data"]?["options"]?.AsArray()
            .FirstOrDefault(o => o?["name"]?.GetValue<string>() == "code")?["value"]?.GetValue<string>();

        if (code is null)
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

    private async Task HandleUnlinkCommandAsync(string interactionId, string interactionToken, string discordUserId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var user = await DiscordLinkService.UnlinkAsync(db, discordUserId, ct);

        if (user is null)
        {
            await RespondAsync(interactionId, interactionToken, "Aucun compte Aetheria n'est lié à ton compte Discord.", ct);
            return;
        }

        await roleSyncService.RevokeAllRolesAsync(discordUserId, ct);
        await RespondAsync(interactionId, interactionToken, $"Compte **{user.Username}** délié. Tes rôles liés à la vérification ont été retirés.", ct);
    }

    private async Task RespondAsync(string interactionId, string interactionToken, string message, CancellationToken ct, bool ephemeral = true)
    {
        try
        {
            var data = new JsonObject { ["content"] = message };
            if (ephemeral)
            {
                data["flags"] = 64; // EPHEMERAL — visible seulement par l'auteur de l'interaction
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"interactions/{interactionId}/{interactionToken}/callback")
            {
                Content = JsonContent.Create(new
                {
                    type = 4, // CHANNEL_MESSAGE_WITH_SOURCE
                    data,
                }),
            };

            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Échec de réponse à une interaction Discord : {Status} {Body}", response.StatusCode, body);
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Impossible de répondre à une interaction Discord.");
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
