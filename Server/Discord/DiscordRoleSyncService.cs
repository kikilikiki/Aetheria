using System.Net.Http.Headers;
using Aetheria.Database.Entities;
using Aetheria.Shared.Enums;
using Microsoft.Extensions.Logging;

namespace Aetheria.Server.Discord;

/// <summary>
/// Attribue automatiquement le rôle Discord correspondant au grade communautaire
/// (<see cref="UserRank"/>) d'un compte lié (voir GDD/demande utilisateur — "système de link le
/// compte discord avec le jeu pour sur discord avoir les rôles de grade automatiquement").
/// Utilise la même approche REST simple (Authorization: Bot &lt;token&gt;) que
/// <see cref="DiscordAnnouncer"/> — pas besoin d'une connexion gateway pour ces appels sortants,
/// contrairement à <see cref="DiscordGatewayClient"/> qui doit lui recevoir les commandes.
/// Un identifiant de rôle par grade est configuré via variable d'environnement
/// (<c>DISCORD_ROLE_ID_&lt;GRADE&gt;</c>), absent = grade non synchronisé (pas d'erreur).
/// <c>DISCORD_VERIFIED_ROLE_ID</c> (optionnel) est un rôle unique accordé dès la première
/// vérification, indépendant du grade — voir demande utilisateur.
/// </summary>
public sealed class DiscordRoleSyncService
{
    private readonly HttpClient _http;
    private readonly string? _botToken;
    private readonly IReadOnlyList<string> _guildIds;
    private readonly IReadOnlyDictionary<UserRank, string> _roleIdsByRank;
    private readonly string? _verifiedRoleId;
    private readonly ILogger<DiscordRoleSyncService> _logger;

    public DiscordRoleSyncService(ILogger<DiscordRoleSyncService> logger)
    {
        _logger = logger;
        _botToken = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");

        // Voir demande utilisateur — rôle unique donné dès qu'un compte est vérifié (lié), en plus
        // du rôle de grade optionnel ci-dessous (DISCORD_ROLE_ID_<GRADE>).
        _verifiedRoleId = Environment.GetEnvironmentVariable("DISCORD_VERIFIED_ROLE_ID");

        // Voir GDD/demande utilisateur — "que le bot sois actif avec le serveur (prod et dev)" :
        // chaque instance (dev/prod) tourne comme processus séparé avec son propre .env (voir
        // start-server-dev.bat/start-server-prod.bat), donc son propre DISCORD_BOT_TOKEN/
        // DISCORD_GUILD_IDS — pas de connexion gateway partagée entre les deux (voir
        // DiscordGatewayClient), chaque instance gère indépendamment son(ses) serveur(s) Discord.
        var guildsEnv = Environment.GetEnvironmentVariable("DISCORD_GUILD_IDS");
        _guildIds = guildsEnv is { Length: > 0 }
            ? guildsEnv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

        _roleIdsByRank = BuildRoleMap();
        _http = new HttpClient { BaseAddress = new Uri("https://discord.com/api/v10/") };
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_botToken) && _guildIds.Count > 0;

    private static Dictionary<UserRank, string> BuildRoleMap()
    {
        var map = new Dictionary<UserRank, string>();
        foreach (var rank in Enum.GetValues<UserRank>())
        {
            var value = Environment.GetEnvironmentVariable($"DISCORD_ROLE_ID_{rank.ToString().ToUpperInvariant()}");
            if (value is { Length: > 0 })
            {
                map[rank] = value;
            }
        }

        return map;
    }

    /// <summary>
    /// Synchronise le rôle Discord du compte avec son grade actuel, dans tous les serveurs Discord
    /// configurés : retire les autres rôles de grade connus puis ajoute celui du grade actuel (si
    /// un rôle est configuré pour ce grade). Ne fait rien si le compte n'est pas lié
    /// (<see cref="UserEntity.DiscordUserId"/> null) ou si le service n'est pas configuré.
    /// N'écrit jamais d'exception vers l'appelant : un échec de synchronisation Discord ne doit
    /// jamais faire échouer une action de jeu (changement de grade, link, ...).
    /// </summary>
    public async Task SyncUserRoleAsync(UserEntity user, CancellationToken ct = default)
    {
        if (!IsConfigured || user.DiscordUserId is not { Length: > 0 } discordUserId)
        {
            return;
        }

        var targetRoleId = _roleIdsByRank.GetValueOrDefault(user.Rank);

        foreach (var guildId in _guildIds)
        {
            // Rôle "vérifié" : jamais retiré une fois accordé, indépendant du grade.
            if (_verifiedRoleId is { Length: > 0 })
            {
                await SendRoleRequestAsync(HttpMethod.Put, guildId, discordUserId, _verifiedRoleId, ct);
            }

            foreach (var (rank, roleId) in _roleIdsByRank)
            {
                if (rank == user.Rank)
                {
                    continue;
                }

                await SendRoleRequestAsync(HttpMethod.Delete, guildId, discordUserId, roleId, ct);
            }

            if (targetRoleId is { Length: > 0 })
            {
                await SendRoleRequestAsync(HttpMethod.Put, guildId, discordUserId, targetRoleId, ct);
            }
        }
    }

    private async Task SendRoleRequestAsync(HttpMethod method, string guildId, string discordUserId, string roleId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, $"guilds/{guildId}/members/{discordUserId}/roles/{roleId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", _botToken);

        try
        {
            var response = await _http.SendAsync(request, ct);
            // 404 attendu si le membre a quitté ce serveur Discord précis (bot présent sur
            // plusieurs guildes) — pas une vraie erreur, juste ignoré silencieusement.
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Échec de synchronisation du rôle Discord {RoleId} ({Method}) pour {DiscordUserId} sur {GuildId} : {Status} {Body}", roleId, method, discordUserId, guildId, response.StatusCode, body);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Impossible de contacter Discord pour synchroniser le rôle {RoleId} de {DiscordUserId}.", roleId, discordUserId);
        }
    }
}
