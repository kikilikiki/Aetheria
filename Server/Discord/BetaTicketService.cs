using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Aetheria.Database.Entities;
using Microsoft.Extensions.Logging;

namespace Aetheria.Server.Discord;

/// <summary>
/// Crée et gère les salons Discord privés (« tickets ») des candidatures bêta soumises depuis le
/// portail web (<c>Aetheria.Web</c>). Le portail ne parle jamais à Discord lui-même (l'IP partagée
/// de son hébergeur — Render gratuit — est rate-limitée par Cloudflare) : il écrit la candidature
/// en base, et <see cref="BetaTicketProcessor"/>, ici, fait le travail Discord depuis la machine
/// qui héberge déjà le bot. Même approche REST minimale que <see cref="DiscordAnnouncer"/>.
/// </summary>
public sealed class BetaTicketService
{
    private const string ApiBase = "https://discord.com/api/v10/";

    // VIEW_CHANNEL (1<<10) | SEND_MESSAGES (1<<11) | READ_MESSAGE_HISTORY (1<<16)
    private const string ViewSendHistory = "68608";
    private const string ViewChannel = "1024";

    private readonly HttpClient _http = new() { BaseAddress = new Uri(ApiBase) };
    private readonly ILogger<BetaTicketService> _logger;

    private readonly string? _botToken;
    private readonly string? _guildId;
    private readonly string _categoryId;
    private readonly IReadOnlyList<string> _staffRoleIds;
    private readonly string? _testerRoleId;

    public string InviteUrl { get; }

    public BetaTicketService(ILogger<BetaTicketService> logger)
    {
        _logger = logger;
        _botToken = Env("DISCORD_BOT_TOKEN");

        _guildId = Env("DISCORD_BETA_GUILD_ID")
            ?? Env("DISCORD_GUILD_IDS")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();

        _categoryId = Env("DISCORD_BETA_CATEGORY_ID") ?? "1531565847125164123";

        _staffRoleIds = (Env("DISCORD_BETA_STAFF_ROLE_IDS") ?? "1531571205805707385,1516429803442671626")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        _testerRoleId = Env("DISCORD_ROLE_ID_TESTEUR")
            ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();

        InviteUrl = Env("DISCORD_INVITE_URL") ?? "https://discord.gg/8NqXPsg7gE";
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_botToken) && !string.IsNullOrWhiteSpace(_guildId);

    private static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name)?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public sealed record MemberResolution(bool Found, string? DiscordUserId, string? Error);

    /// <summary>
    /// Confirme la présence du candidat sur le serveur Discord. Si son compte est déjà lié
    /// (<see cref="UserEntity.DiscordUserId"/>) on vérifie l'appartenance directement ; sinon on
    /// recherche le pseudo saisi parmi les membres.
    /// </summary>
    public async Task<MemberResolution> ResolveMemberAsync(string? linkedDiscordUserId, string typedHandle, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return new MemberResolution(false, null, "vérification Discord non configurée côté serveur de jeu");
        }

        if (!string.IsNullOrWhiteSpace(linkedDiscordUserId))
        {
            var linked = await SendAsync(HttpMethod.Get, $"guilds/{_guildId}/members/{linkedDiscordUserId}", null, ct);
            if (linked is { IsSuccessStatusCode: true })
            {
                return new MemberResolution(true, linkedDiscordUserId, null);
            }

            return new MemberResolution(false, null, "ton compte Discord lié n'est pas (ou plus) sur le serveur");
        }

        var handle = typedHandle.Trim().TrimStart('@');
        if (handle.Length < 2)
        {
            return new MemberResolution(false, null, "pseudo Discord manquant");
        }

        var search = await SendAsync(HttpMethod.Get, $"guilds/{_guildId}/members/search?query={Uri.EscapeDataString(handle)}&limit=10", null, ct);
        if (search is null || !search.IsSuccessStatusCode)
        {
            var body = search is null ? "(pas de réponse)" : await search.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Recherche membre Discord échouée : {Status} {Body}", search?.StatusCode, body);
            return new MemberResolution(false, null, "erreur temporaire de vérification Discord");
        }

        var results = await search.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var normalized = handle.ToLowerInvariant();

        foreach (var entry in results.EnumerateArray())
        {
            if (!entry.TryGetProperty("user", out var discordUser))
            {
                continue;
            }

            var username = discordUser.TryGetProperty("username", out var u) ? u.GetString() : null;
            var globalName = discordUser.TryGetProperty("global_name", out var g) ? g.GetString() : null;
            var nick = entry.TryGetProperty("nick", out var n) ? n.GetString() : null;

            if (Eq(username, normalized) || Eq(globalName, normalized) || Eq(nick, normalized))
            {
                return new MemberResolution(true, discordUser.GetProperty("id").GetString(), null);
            }
        }

        return new MemberResolution(false, null, "introuvable sur le serveur Discord (pseudo mal orthographié ou serveur pas rejoint)");
    }

    private static bool Eq(string? a, string b) => !string.IsNullOrEmpty(a) && a.ToLowerInvariant() == b;

    /// <summary>Crée le salon du ticket et y poste le récapitulatif. Retourne l'ID du salon, ou <c>null</c>.</summary>
    public async Task<string?> CreateTicketAsync(BetaApplicationEntity application, string applicantDiscordId, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return null;
        }

        var overwrites = new List<object>
        {
            new { id = _guildId, type = 0, deny = ViewChannel },
            new { id = applicantDiscordId, type = 1, allow = ViewSendHistory },
        };
        overwrites.AddRange(_staffRoleIds.Select(r => new { id = r, type = 0, allow = ViewSendHistory }));

        var pseudo = application.InGamePseudo.Length > 0 ? application.InGamePseudo : application.Username;

        var create = await SendAsync(HttpMethod.Post, $"guilds/{_guildId}/channels", new
        {
            name = $"beta-test-{Slug(pseudo)}",
            type = 0,
            parent_id = _categoryId,
            topic = $"Candidature bêta de {application.Username} — {application.CreatedAtUtc:yyyy-MM-dd}",
            permission_overwrites = overwrites,
        }, ct);

        if (create is null || !create.IsSuccessStatusCode)
        {
            var body = create is null ? "(pas de réponse)" : await create.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Création ticket bêta échouée : {Status} {Body}", create?.StatusCode, body);
            return null;
        }

        var channel = await create.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var channelId = channel.GetProperty("id").GetString();
        if (channelId is null)
        {
            return null;
        }

        var mentions = string.Join(' ', _staffRoleIds.Select(r => $"<@&{r}>"));
        await SendAsync(HttpMethod.Post, $"channels/{channelId}/messages", new
        {
            content = $"<@{applicantDiscordId}> {mentions}".Trim(),
            embeds = new[]
            {
                new
                {
                    title = "Nouvelle candidature bêta",
                    color = 0xA8353A,
                    fields = new object[]
                    {
                        new { name = "Compte Aetheria", value = application.Username, inline = true },
                        new { name = "Pseudo en jeu", value = Blank(application.InGamePseudo), inline = true },
                        new { name = "Plateforme", value = Blank(application.Platform), inline = true },
                        new { name = "Discord", value = Blank(application.DiscordHandle), inline = true },
                        new { name = "Email", value = Blank(application.ContactEmail), inline = true },
                        new { name = "Configuration PC", value = Truncate(Blank(application.HardwareSpecs), 1024) },
                        new { name = "Remarques", value = Truncate(Blank(application.Notes), 1024) },
                    },
                    timestamp = application.CreatedAtUtc.ToString("o"),
                },
            },
            allowed_mentions = new { users = new[] { applicantDiscordId }, roles = _staffRoleIds },
        }, ct);

        return channelId;
    }

    public async Task PostToTicketAsync(string channelId, string message, CancellationToken ct)
    {
        var response = await SendAsync(HttpMethod.Post, $"channels/{channelId}/messages", new
        {
            content = message,
            allowed_mentions = new { parse = Array.Empty<string>() },
        }, ct);

        if (response is not null && !response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Message ticket bêta échoué ({Status}).", response.StatusCode);
        }
    }

    public async Task GrantTesterRoleAsync(string discordUserId, CancellationToken ct)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(_testerRoleId))
        {
            return;
        }

        await SendAsync(HttpMethod.Put, $"guilds/{_guildId}/members/{discordUserId}/roles/{_testerRoleId}", null, ct);
    }

    private async Task<HttpResponseMessage?> SendAsync(HttpMethod method, string path, object? jsonBody, CancellationToken ct)
    {
        HttpResponseMessage? response = null;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bot", _botToken);
            if (jsonBody is not null)
            {
                request.Content = JsonContent.Create(jsonBody);
            }

            try
            {
                response?.Dispose();
                response = await _http.SendAsync(request, ct);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Appel Discord impossible ({Method} {Path}).", method, path);
                return null;
            }

            if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt == 4)
            {
                return response;
            }

            var delay = response.Headers.RetryAfter?.Delta
                ?? (double.TryParse(response.Headers.TryGetValues("Retry-After", out var v) ? v.FirstOrDefault() : null,
                        System.Globalization.CultureInfo.InvariantCulture, out var s)
                    ? TimeSpan.FromSeconds(s)
                    : TimeSpan.FromSeconds(3));

            var wait = delay > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : delay + TimeSpan.FromMilliseconds(250);
            _logger.LogInformation("Discord 429 sur {Path}, nouvelle tentative dans {Wait}.", path, wait);
            try
            {
                await Task.Delay(wait, ct);
            }
            catch (OperationCanceledException)
            {
                return response;
            }
        }

        return response;
    }

    private static string Blank(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : string.Concat(value.AsSpan(0, max - 1), "…");

    public static string Slug(string value)
    {
        var builder = new StringBuilder();
        foreach (var c in value.Trim().ToLowerInvariant())
        {
            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                builder.Append(c);
            }
            else if (c is ' ' or '-' or '_' or '.' && builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        if (slug.Length > 90)
        {
            slug = slug[..90].Trim('-');
        }

        return slug.Length == 0 ? "candidat" : slug;
    }
}
