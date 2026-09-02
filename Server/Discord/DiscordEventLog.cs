using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Aetheria.Server.Discord;

/// <summary>
/// Poste des lignes de journal dans des salons Discord dédiés (voir demande utilisateur) :
/// <list type="bullet">
///   <item><see cref="LogReferral"/> → un salon « inscriptions » : pseudo (id) + code de parrainage
///     de chaque nouveau testeur.</item>
///   <item><see cref="LogMatch"/> → un salon « résultats de matchs » : chaque duel amical / classé,
///     qui contre qui (pseudos en jeu), et les points ELO gagnés/perdus si c'était classé.</item>
/// </list>
/// Best-effort, tir-et-oublie : un échec Discord n'interrompt jamais le jeu. Réutilise le même
/// <c>DISCORD_BOT_TOKEN</c> que le reste (le serveur de jeu tourne sur une IP non rate-limitée).
/// </summary>
public static class DiscordEventLog
{
    private static readonly HttpClient Http = new() { BaseAddress = new Uri("https://discord.com/api/v10/") };

    private static string? Token => Trim(Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN"));

    private static string ReferralChannelId => Trim(Environment.GetEnvironmentVariable("DISCORD_REFERRAL_LOG_CHANNEL_ID")) ?? "1544780312171511828";
    private static string MatchChannelId => Trim(Environment.GetEnvironmentVariable("DISCORD_MATCH_LOG_CHANNEL_ID")) ?? "1544780577666764911";

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static void LogReferral(string username, Guid userId, string referralCode) =>
        _ = PostAsync(ReferralChannelId, $"📝 **{username}** (`{userId}`) — code de parrainage : `{referralCode}`");

    public static void LogMatch(string line) =>
        _ = PostAsync(MatchChannelId, line);

    private static async Task PostAsync(string channelId, string content)
    {
        if (Token is null)
        {
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"channels/{channelId}/messages");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bot", Token);
            request.Content = JsonContent.Create(new
            {
                content = content.Length > 1900 ? content[..1900] : content,
                allowed_mentions = new { parse = Array.Empty<string>() },
            });

            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"[DiscordEventLog] échec canal {channelId} : {(int)response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[DiscordEventLog] {ex.Message}");
        }
    }
}
