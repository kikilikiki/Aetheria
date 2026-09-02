using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Aetheria.Database.Entities;

namespace Aetheria.Web.Services;

/// <summary>
/// Crée et gère les salons Discord privés (« tickets ») associés aux candidatures bêta. Client
/// REST minimal (<c>Authorization: Bot &lt;token&gt;</c>), même approche que
/// <c>Server/Discord/DiscordAnnouncer.cs</c> — aucune connexion Gateway, seulement des appels
/// sortants. Utilise le MÊME bot que le serveur de jeu (<c>DISCORD_BOT_TOKEN</c>) ; le bot doit
/// avoir la permission « Gérer les salons » dans la guilde visée, et l'intent privilégié
/// « Server Members » activé (nécessaire à la recherche de membre).
/// </summary>
public sealed class DiscordTicketService
{
    private const string ApiBase = "https://discord.com/api/v10/";

    // VIEW_CHANNEL (1&lt;&lt;10) | SEND_MESSAGES (1&lt;&lt;11) | READ_MESSAGE_HISTORY (1&lt;&lt;16)
    private const string ViewSendHistory = "68608";
    private const string ViewChannel = "1024";

    private readonly HttpClient _http = new() { BaseAddress = new Uri(ApiBase) };
    private readonly ILogger<DiscordTicketService> _logger;

    private readonly string? _botToken;
    private readonly string? _guildId;
    private readonly string _categoryId;
    private readonly IReadOnlyList<string> _staffRoleIds;
    private readonly string? _testerRoleId;

    public string InviteUrl { get; }

    public DiscordTicketService(ILogger<DiscordTicketService> logger)
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

    /// <summary>Vrai si le bot est configuré (token + guilde) — sinon les tickets sont désactivés.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_botToken) && !string.IsNullOrWhiteSpace(_guildId);

    public string? GuildId => _guildId;

    private static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name)?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    // --- Résolution du membre Discord --------------------------------------------------------

    public sealed record MemberResolution(bool Found, string? DiscordUserId, string? DisplayName, string? Error);

    /// <summary>
    /// Confirme que le candidat est bien membre du serveur Discord. Si son compte Aetheria est déjà
    /// lié (<see cref="UserEntity.DiscordUserId"/>), on vérifie directement l'appartenance ; sinon
    /// on recherche le pseudo saisi parmi les membres de la guilde.
    /// </summary>
    public async Task<MemberResolution> ResolveMemberAsync(UserEntity user, string typedHandle, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return new MemberResolution(false, null, null, "La vérification Discord n'est pas configurée sur le serveur. Contacte un administrateur.");
        }

        // 1. Compte déjà lié via /link en jeu → vérification fiable par identité Discord.
        if (!string.IsNullOrWhiteSpace(user.DiscordUserId))
        {
            var linked = await SendAsync(HttpMethod.Get, $"guilds/{_guildId}/members/{user.DiscordUserId}", null, ct);
            if (linked is { IsSuccessStatusCode: true })
            {
                var member = await linked.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                return new MemberResolution(true, user.DiscordUserId, ExtractDisplayName(member), null);
            }

            return new MemberResolution(false, null, null,
                $"Ton compte Discord lié n'est pas (ou plus) sur le serveur. Rejoins-le puis réessaie : {InviteUrl}");
        }

        // 2. Recherche par pseudo saisi.
        var handle = typedHandle.Trim().TrimStart('@');
        if (handle.Length < 2)
        {
            return new MemberResolution(false, null, null, "Indique ton pseudo Discord.");
        }

        var search = await SendAsync(HttpMethod.Get, $"guilds/{_guildId}/members/search?query={Uri.EscapeDataString(handle)}&limit=10", null, ct);
        if (search is null || !search.IsSuccessStatusCode)
        {
            var status = search?.StatusCode;
            var body = search is null ? "(pas de réponse)" : await search.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Recherche de membre Discord échouée : status={Status}, guildId={GuildId}, body={Body}", status, _guildId, body);

            var userMessage = status switch
            {
                HttpStatusCode.TooManyRequests => "Trop de vérifications en cours. Réessaie dans une minute.",
                HttpStatusCode.Unauthorized => "Le jeton du bot Discord est invalide côté serveur. Contacte un administrateur.",
                HttpStatusCode.Forbidden => "Le bot Discord n'a pas accès à la liste des membres (intent « Server Members » à activer). Contacte un administrateur.",
                HttpStatusCode.NotFound => "Le serveur Discord configuré est introuvable. Contacte un administrateur.",
                _ => $"Vérification Discord indisponible ({status}). Réessaie plus tard.",
            };

            return new MemberResolution(false, null, null, userMessage);
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

            if (Matches(username, normalized) || Matches(globalName, normalized) || Matches(nick, normalized))
            {
                var id = discordUser.GetProperty("id").GetString();
                return new MemberResolution(true, id, globalName ?? username, null);
            }
        }

        return new MemberResolution(false, null, null,
            $"Impossible de te trouver sur le serveur Discord. Vérifie l'orthographe de ton pseudo Discord (utilise ton nom d'utilisateur, pas un surnom), ou rejoins d'abord le serveur : {InviteUrl}");
    }

    private static bool Matches(string? candidate, string normalized) =>
        !string.IsNullOrEmpty(candidate) && candidate.ToLowerInvariant() == normalized;

    private static string? ExtractDisplayName(JsonElement member)
    {
        if (member.TryGetProperty("nick", out var nick) && nick.ValueKind == JsonValueKind.String)
        {
            return nick.GetString();
        }

        if (member.TryGetProperty("user", out var user))
        {
            return (user.TryGetProperty("global_name", out var g) ? g.GetString() : null)
                ?? (user.TryGetProperty("username", out var u) ? u.GetString() : null);
        }

        return null;
    }

    // --- Création / gestion du ticket -------------------------------------------------------

    /// <summary>
    /// Crée le salon <c>beta-test-&lt;pseudo&gt;</c> dans la catégorie configurée, visible seulement
    /// par les rôles staff et le candidat, puis y poste un récapitulatif. Retourne l'identifiant du
    /// salon, ou <c>null</c> si la création échoue (le candidat est alors quand même enregistré).
    /// </summary>
    public async Task<string?> CreateTicketAsync(BetaApplicationEntity application, string applicantDiscordId, CancellationToken ct = default)
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
        overwrites.AddRange(_staffRoleIds.Select(roleId => new { id = roleId, type = 0, allow = ViewSendHistory }));

        var createBody = new
        {
            name = $"beta-test-{Slug(application.InGamePseudo.Length > 0 ? application.InGamePseudo : application.Username)}",
            type = 0,
            parent_id = _categoryId,
            topic = $"Candidature bêta de {application.Username} — {application.CreatedAtUtc:yyyy-MM-dd}",
            permission_overwrites = overwrites,
        };

        var createResponse = await SendAsync(HttpMethod.Post, $"guilds/{_guildId}/channels", createBody, ct);
        if (createResponse is null || !createResponse.IsSuccessStatusCode)
        {
            var body = createResponse is null ? "(pas de réponse)" : await createResponse.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Création du ticket Discord échouée ({Status}) : {Body}", createResponse?.StatusCode, body);
            return null;
        }

        var channel = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var channelId = channel.GetProperty("id").GetString();
        if (channelId is null)
        {
            return null;
        }

        var mentions = string.Join(' ', _staffRoleIds.Select(r => $"<@&{r}>"));
        var content = $"<@{applicantDiscordId}> {mentions}".Trim();

        var embed = new
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
        };

        await SendAsync(HttpMethod.Post, $"channels/{channelId}/messages", new
        {
            content,
            embeds = new[] { embed },
            allowed_mentions = new { users = new[] { applicantDiscordId }, roles = _staffRoleIds },
        }, ct);

        return channelId;
    }

    /// <summary>Poste un message simple dans un ticket existant (validation / refus par le staff).</summary>
    public async Task PostToTicketAsync(string channelId, string message, CancellationToken ct = default)
    {
        var response = await SendAsync(HttpMethod.Post, $"channels/{channelId}/messages", new
        {
            content = message,
            allowed_mentions = new { parse = Array.Empty<string>() },
        }, ct);

        if (response is not null && !response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Message Discord échoué dans {ChannelId} ({Status}) : {Body}", channelId, response.StatusCode, body);
        }
    }

    /// <summary>Supprime (archive) le salon d'un ticket.</summary>
    public async Task<bool> ArchiveTicketAsync(string channelId, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return false;
        }

        var response = await SendAsync(HttpMethod.Delete, $"channels/{channelId}", null, ct);
        return response is { IsSuccessStatusCode: true };
    }

    /// <summary>Attribue le rôle Discord « Testeur » au candidat accepté (best-effort, si configuré).</summary>
    public async Task GrantTesterRoleAsync(string discordUserId, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(_testerRoleId))
        {
            return;
        }

        await SendAsync(HttpMethod.Put, $"guilds/{_guildId}/members/{discordUserId}/roles/{_testerRoleId}", null, ct);
    }

    public string TicketUrl(string channelId) => $"https://discord.com/channels/{_guildId}/{channelId}";

    /// <summary>
    /// Envoie une requête à l'API Discord avec le jeton du bot, en réessayant sur 429 (respecte
    /// l'en-tête <c>Retry-After</c>, plafonné). La requête est reconstruite à chaque tentative.
    /// Retourne <c>null</c> si l'appel réseau échoue.
    /// </summary>
    private async Task<HttpResponseMessage?> SendAsync(HttpMethod method, string path, object? jsonBody, CancellationToken ct)
    {
        HttpResponseMessage? response = null;

        for (var attempt = 0; attempt < 4; attempt++)
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
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("Appel Discord expiré ({Method} {Path}).", method, path);
                return null;
            }

            if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt == 3)
            {
                return response;
            }

            var delay = response.Headers.RetryAfter?.Delta
                ?? (double.TryParse(response.Headers.TryGetValues("Retry-After", out var v) ? v.FirstOrDefault() : null,
                        System.Globalization.CultureInfo.InvariantCulture, out var seconds)
                    ? TimeSpan.FromSeconds(seconds)
                    : TimeSpan.FromSeconds(2));

            var wait = delay > TimeSpan.FromSeconds(8) ? TimeSpan.FromSeconds(8) : delay;
            _logger.LogInformation("Discord 429 sur {Path}, nouvelle tentative dans {Wait}.", path, wait);
            try
            {
                await Task.Delay(wait, ct);
            }
            catch (TaskCanceledException)
            {
                return response;
            }
        }

        return response;
    }

    private static string Blank(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : string.Concat(value.AsSpan(0, max - 1), "…");

    /// <summary>Nom de salon Discord valide : minuscules, chiffres et tirets uniquement.</summary>
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
