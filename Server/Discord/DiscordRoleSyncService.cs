using System.Net.Http.Headers;
using Aetheria.Database.Entities;
using Aetheria.Shared.Enums;
using Microsoft.Extensions.Logging;

namespace Aetheria.Server.Discord;

/// <summary>
/// Attribue automatiquement le(s) rôle(s) Discord correspondant au grade communautaire
/// (<see cref="UserRank"/>) d'un compte lié (voir GDD/demande utilisateur — "système de link le
/// compte discord avec le jeu pour sur discord avoir les rôles de grade automatiquement").
/// Utilise la même approche REST simple (Authorization: Bot &lt;token&gt;) que
/// <see cref="DiscordAnnouncer"/> — pas besoin d'une connexion gateway pour ces appels sortants,
/// contrairement à <see cref="DiscordGatewayClient"/> qui doit lui recevoir les commandes.
/// Un ou plusieurs identifiants de rôle par grade sont configurés via variable d'environnement
/// (<c>DISCORD_ROLE_ID_&lt;GRADE&gt;</c>, séparés par des virgules — voir demande utilisateur
/// "sa fait 2 role"), absent = grade non synchronisé (pas d'erreur).
/// <c>DISCORD_VERIFIED_ROLE_ID</c> (optionnel, même format multi-valeurs) accorde un ou plusieurs
/// rôles fixes dès la première vérification, indépendants du grade.
/// </summary>
public sealed class DiscordRoleSyncService
{
    private readonly HttpClient _http;
    private readonly string? _botToken;
    private readonly IReadOnlyList<string> _guildIds;
    private readonly IReadOnlyDictionary<UserRank, IReadOnlyList<string>> _roleIdsByRank;
    private readonly IReadOnlyList<string> _verifiedRoleIds;
    private readonly ILogger<DiscordRoleSyncService> _logger;

    public DiscordRoleSyncService(ILogger<DiscordRoleSyncService> logger)
    {
        _logger = logger;
        _botToken = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");

        // Voir demande utilisateur — un ou plusieurs rôles fixes donnés dès qu'un compte est
        // vérifié (lié), en plus du/des rôle(s) de grade optionnel(s) ci-dessous (DISCORD_ROLE_ID_<GRADE>).
        _verifiedRoleIds = ParseRoleIds(Environment.GetEnvironmentVariable("DISCORD_VERIFIED_ROLE_ID"));

        // Voir GDD/demande utilisateur — "que le bot sois actif avec le serveur (prod et dev)" :
        // chaque instance (dev/prod) tourne comme processus séparé avec son propre .env (voir
        // start-server-dev.bat/start-server-prod.bat), donc son propre DISCORD_BOT_TOKEN/
        // DISCORD_GUILD_IDS — pas de connexion gateway partagée entre les deux (voir
        // DiscordGatewayClient), chaque instance gère indépendamment son(ses) serveur(s) Discord.
        _guildIds = ParseRoleIds(Environment.GetEnvironmentVariable("DISCORD_GUILD_IDS"));

        _roleIdsByRank = BuildRoleMap();
        _http = new HttpClient { BaseAddress = new Uri("https://discord.com/api/v10/") };
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_botToken) && _guildIds.Count > 0;

    private static List<string> ParseRoleIds(string? value) =>
        value is { Length: > 0 }
            ? value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : [];

    private static Dictionary<UserRank, IReadOnlyList<string>> BuildRoleMap()
    {
        var map = new Dictionary<UserRank, IReadOnlyList<string>>();
        foreach (var rank in Enum.GetValues<UserRank>())
        {
            var roleIds = ParseRoleIds(Environment.GetEnvironmentVariable($"DISCORD_ROLE_ID_{rank.ToString().ToUpperInvariant()}"));
            if (roleIds.Count > 0)
            {
                map[rank] = roleIds;
            }
        }

        return map;
    }

    /// <summary>
    /// Synchronise le(s) rôle(s) Discord du compte avec son grade actuel, dans tous les serveurs
    /// Discord configurés. Voir demande utilisateur — "donne le role de tout les joueur au gens
    /// qui link leur discord" : le(s) rôle(s) <see cref="UserRank.Joueur"/> forment un socle commun
    /// à tous les comptes vérifiés, accordé une fois pour toutes et jamais retiré (même logique que
    /// le(s) rôle(s) <c>DISCORD_VERIFIED_ROLE_ID</c>) — contrairement aux rôles de grade supérieurs
    /// (VIP/Testeur/Ami/Modérateur/Fondateur), mutuellement exclusifs entre eux (le rôle de grade
    /// précédent est retiré quand un nouveau est accordé, ou en cas de rétrogradation vers
    /// Joueur). Ne fait rien si le compte n'est pas lié (<see cref="UserEntity.DiscordUserId"/>
    /// null) ou si le service n'est pas configuré. N'écrit jamais d'exception vers l'appelant : un
    /// échec de synchronisation Discord ne doit jamais faire échouer une action de jeu (changement
    /// de grade, link, ...).
    /// </summary>
    public async Task SyncUserRoleAsync(UserEntity user, CancellationToken ct = default)
    {
        if (!IsConfigured || user.DiscordUserId is not { Length: > 0 } discordUserId)
        {
            return;
        }

        var baseRoleIds = _roleIdsByRank.GetValueOrDefault(UserRank.Joueur, []);
        var higherRankRoleIds = user.Rank != UserRank.Joueur ? _roleIdsByRank.GetValueOrDefault(user.Rank, []) : [];

        foreach (var guildId in _guildIds)
        {
            // Rôle(s) "vérifié" et rôle(s) de base Joueur : jamais retirés une fois accordés.
            foreach (var verifiedRoleId in _verifiedRoleIds)
            {
                await SendRoleRequestAsync(HttpMethod.Put, guildId, discordUserId, verifiedRoleId, ct);
            }

            foreach (var baseRoleId in baseRoleIds)
            {
                await SendRoleRequestAsync(HttpMethod.Put, guildId, discordUserId, baseRoleId, ct);
            }

            foreach (var (rank, roleIds) in _roleIdsByRank)
            {
                if (rank == UserRank.Joueur || rank == user.Rank)
                {
                    continue;
                }

                foreach (var roleId in roleIds)
                {
                    await SendRoleRequestAsync(HttpMethod.Delete, guildId, discordUserId, roleId, ct);
                }
            }

            foreach (var higherRankRoleId in higherRankRoleIds)
            {
                await SendRoleRequestAsync(HttpMethod.Put, guildId, discordUserId, higherRankRoleId, ct);
            }
        }
    }

    /// <summary>
    /// Voir demande utilisateur — "ajoute une commande de unlink" : retire tous les rôles gérés
    /// par ce service (vérifié + tous les grades connus) d'un compte Discord qui vient d'être
    /// délié, dans tous les serveurs configurés. Ne fait rien si le service n'est pas configuré.
    /// </summary>
    public async Task RevokeAllRolesAsync(string discordUserId, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return;
        }

        foreach (var guildId in _guildIds)
        {
            foreach (var verifiedRoleId in _verifiedRoleIds)
            {
                await SendRoleRequestAsync(HttpMethod.Delete, guildId, discordUserId, verifiedRoleId, ct);
            }

            foreach (var roleId in _roleIdsByRank.Values.SelectMany(ids => ids))
            {
                await SendRoleRequestAsync(HttpMethod.Delete, guildId, discordUserId, roleId, ct);
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
