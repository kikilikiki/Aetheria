using System.Net.Http.Headers;
using System.Net.Http.Json;
using Aetheria.Shared;
using Microsoft.Extensions.Logging;

namespace Aetheria.Server.Discord;

/// <summary>
/// Poste des annonces de mise à jour dans un ou plusieurs salons Discord fixes, via l'API REST
/// des bots (Authorization: Bot &lt;token&gt;) plutôt qu'une connexion gateway complète —
/// suffisant pour de simples messages sortants, pas besoin de recevoir d'évènements Discord. Le
/// jeton vient de la variable d'environnement <c>DISCORD_BOT_TOKEN</c> (chargée depuis
/// <c>.env</c> si présent, voir <c>DotEnv.Load</c>) : jamais commité, voir <c>.env.exemple</c>
/// pour le modèle attendu. "Hébergé" par le processus Aetheria.Server lui-même (pas de bot séparé
/// à faire tourner en permanence) — voir Docs/README.md.
/// </summary>
public sealed class DiscordAnnouncer
{
    /// <summary>Salons visés (voir demande utilisateur — plusieurs salons) — surchargeable via DISCORD_ANNOUNCE_CHANNEL_IDS (séparés par des virgules).</summary>
    private static readonly string[] DefaultChannelIds = ["1531570097582510141", "1531681223926218984"];

    /// <summary>Rôle notifié à chaque annonce (voir demande utilisateur) — surchargeable via DISCORD_ANNOUNCE_ROLE_ID.</summary>
    private const string DefaultRoleId = "1516429837168934942";

    private readonly HttpClient _http;
    private readonly string? _botToken;
    private readonly IReadOnlyList<string> _channelIds;
    private readonly string _roleId;
    private readonly ILogger<DiscordAnnouncer> _logger;

    public DiscordAnnouncer(ILogger<DiscordAnnouncer> logger)
    {
        _logger = logger;
        _botToken = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");

        // Accepte l'ancienne variable DISCORD_ANNOUNCE_CHANNEL_ID (un seul salon) pour ne pas
        // casser une configuration .env existante, en plus de la nouvelle variable au pluriel.
        var channelsEnv = Environment.GetEnvironmentVariable("DISCORD_ANNOUNCE_CHANNEL_IDS")
            ?? Environment.GetEnvironmentVariable("DISCORD_ANNOUNCE_CHANNEL_ID");

        _channelIds = channelsEnv is { Length: > 0 }
            ? channelsEnv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : DefaultChannelIds;

        _roleId = Environment.GetEnvironmentVariable("DISCORD_ANNOUNCE_ROLE_ID") is { Length: > 0 } customRole
            ? customRole
            : DefaultRoleId;

        _http = new HttpClient { BaseAddress = new Uri("https://discord.com/api/v10/") };
    }

    /// <summary>Faux si DISCORD_BOT_TOKEN n'est pas configuré — les annonces sont alors journalisées et ignorées.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_botToken);

    /// <summary>Limite Discord pour la valeur d'un champ d'embed (voir GDD/demande utilisateur — "quand il y a trop de modifications le message ajoute '..' à la fin mais on n'a jamais la suite").</summary>
    private const int FieldValueMaxLength = 1024;

    /// <summary>Limite Discord au nombre d'embeds par message.</summary>
    private const int MaxEmbedsPerMessage = 10;

    /// <summary>Poste dans tous les salons configurés (voir demande utilisateur — plusieurs salons) ; retourne vrai si l'envoi a réussi dans au moins un salon.</summary>
    public async Task<bool> PostUpdateAsync(string title, string description, IReadOnlyList<string> changes, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("DISCORD_BOT_TOKEN absent : annonce Discord ignorée ({Title}).", title);
            return false;
        }

        var truncatedTitle = Truncate(title, 256);
        var truncatedDescription = Truncate(description, 4096);

        // Voir GDD/demande utilisateur — auparavant un seul champ "Changements" plafonné à 1024
        // caractères (limite Discord) : au-delà, le reste de la liste était silencieusement perdu
        // ("..." final sans suite). Découpée maintenant en plusieurs embeds (un message Discord
        // peut en contenir jusqu'à 10), chacun numéroté dans son titre plutôt que dans le corps
        // (Discord ne permet pas de mettre en forme un embed autrement).
        var changeChunks = ChunkChanges(changes);
        var embeds = new List<object>();

        if (changeChunks.Count == 0)
        {
            embeds.Add(BuildEmbed(truncatedTitle, truncatedDescription, null));
        }
        else
        {
            for (var i = 0; i < changeChunks.Count; i++)
            {
                var embedTitle = changeChunks.Count > 1 ? $"{truncatedTitle} ({i + 1}/{changeChunks.Count})" : truncatedTitle;
                embeds.Add(BuildEmbed(embedTitle, i == 0 ? truncatedDescription : null, changeChunks[i]));
            }
        }

        var anySucceeded = false;
        foreach (var channelId in _channelIds)
        {
            // Une seule requête si ça tient dans les 10 embeds autorisés par message ; sinon
            // plusieurs messages successifs (cas extrême, très rare en pratique).
            for (var offset = 0; offset < embeds.Count; offset += MaxEmbedsPerMessage)
            {
                var batch = embeds.Skip(offset).Take(MaxEmbedsPerMessage).ToArray();
                if (await PostToChannelAsync(channelId, batch, includeContent: offset == 0, ct))
                {
                    anySucceeded = true;
                }
            }
        }

        return anySucceeded;
    }

    private static Dictionary<string, object?> BuildEmbed(string title, string? description, string? changesFieldValue)
    {
        var embed = new Dictionary<string, object?>
        {
            ["title"] = title,
            ["color"] = 0x8A5CF6,
            ["footer"] = new { text = $"{GameInfo.Name} v{GameInfo.Version}" },
            ["timestamp"] = DateTime.UtcNow.ToString("o"),
        };

        if (description is not null)
        {
            embed["description"] = description;
        }

        if (changesFieldValue is not null)
        {
            embed["fields"] = new[] { new { name = "Changements", value = changesFieldValue } };
        }

        return embed;
    }

    /// <summary>
    /// Découpe la liste de changements en morceaux qui tiennent chacun dans <see cref="FieldValueMaxLength"/>,
    /// à la frontière d'une puce plutôt qu'en plein milieu (une puce trop longue à elle seule est
    /// quand même tronquée, cas limite improbable en pratique).
    /// </summary>
    private static List<string> ChunkChanges(IReadOnlyList<string> changes)
    {
        var chunks = new List<string>();
        if (changes.Count == 0)
        {
            return chunks;
        }

        var current = new List<string>();
        var currentLength = 0;

        foreach (var change in changes)
        {
            var bullet = $"• {change}";
            var addedLength = bullet.Length + (current.Count > 0 ? 1 : 0); // +1 pour le "\n" séparateur

            if (current.Count > 0 && currentLength + addedLength > FieldValueMaxLength)
            {
                chunks.Add(string.Join('\n', current));
                current = [];
                currentLength = 0;
                addedLength = bullet.Length;
            }

            current.Add(Truncate(bullet, FieldValueMaxLength));
            currentLength += addedLength;
        }

        if (current.Count > 0)
        {
            chunks.Add(string.Join('\n', current));
        }

        return chunks;
    }

    private async Task<bool> PostToChannelAsync(string channelId, IReadOnlyList<object> embeds, bool includeContent, CancellationToken ct)
    {
        // Le ping de rôle doit être dans "content" (un embed seul ne notifie personne) — voir
        // demande utilisateur. allowed_mentions.roles liste explicitement le rôle autorisé à être
        // mentionné : par défaut Discord bloque les mentions de rôle "@everyone-like" venant d'un
        // bot sauf si elles sont explicitement permises ici. Seul le premier message d'un lot
        // ping le rôle (voir includeContent) pour ne pas spammer une notification par embed.
        using var request = new HttpRequestMessage(HttpMethod.Post, $"channels/{channelId}/messages")
        {
            Content = JsonContent.Create(new
            {
                content = includeContent ? $"<@&{_roleId}>" : string.Empty,
                embeds,
                allowed_mentions = new { roles = new[] { _roleId } },
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", _botToken);

        try
        {
            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Échec de l'annonce Discord dans le salon {ChannelId} ({Status}) : {Body}", channelId, response.StatusCode, body);
                return false;
            }

            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Impossible de contacter Discord pour l'annonce dans le salon {ChannelId}.", channelId);
            return false;
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 1), "…");
}
