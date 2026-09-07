using System.Net.Http.Json;
using Aetheria.Database.Entities;
using Aetheria.Shared.Enums;

namespace Aetheria.Web.Services;

/// <summary>
/// Prévient un salon Discord (webhook entrant) à la création d'un code cadeau — voir demande
/// utilisateur : « quand un code est créé, un message dans ce salon pour prévenir avec ce qu'il
/// offre ». L'URL du webhook vit dans <c>DISCORD_GIFTCODE_WEBHOOK_URL</c> (secret, réglé sur
/// Render) ; absente ⇒ rien n'est envoyé. Un webhook ne nécessite pas le jeton du bot et ne fait
/// qu'un seul POST peu fréquent (création de code) — pas concerné par le rate-limit qui a fait
/// déporter les appels du bot sur le serveur de jeu. Tir-et-oublie : un échec d'envoi ne fait
/// jamais échouer la création du code.
/// </summary>
public static class GiftCodeWebhook
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISCORD_GIFTCODE_WEBHOOK_URL"));

    /// <param name="monsterName">Nom de l'espèce offerte (déjà résolu par l'appelant), ou null.</param>
    public static void AnnounceCreated(GiftCodeEntity code, string? monsterName, string createdBy)
    {
        var url = Environment.GetEnvironmentVariable("DISCORD_GIFTCODE_WEBHOOK_URL");
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        var rewards = new List<string>();
        if (code.RewardGems != 0) rewards.Add($"💎 **{code.RewardGems}** gemme(s)");
        if (code.RewardGold != 0) rewards.Add($"🪙 **{code.RewardGold}** or");
        if (code.RewardMonsterSpeciesId is not null)
        {
            var variant = code.RewardMonsterVariant != MonsterVariant.Normal ? $" ({code.RewardMonsterVariant})" : "";
            rewards.Add($"🐾 **{monsterName ?? "Créature"}** niv. {code.RewardMonsterLevel}{variant}");
        }
        if (!string.IsNullOrWhiteSpace(code.Description)) rewards.Add($"✨ {code.Description}");

        var limits = new List<string>
        {
            code.MaxRedemptions is { } m ? $"{m} utilisation(s) max" : "utilisations illimitées",
        };
        if (code.ExpiresAtUtc is { } e) limits.Add($"expire le {e:dd/MM/yyyy}");

        var payload = new
        {
            embeds = new[]
            {
                new
                {
                    title = $"🎁 Nouveau code cadeau : {code.Code}",
                    description = "**Récompense :**\n" + (rewards.Count > 0 ? string.Join("\n", rewards) : "_(aucune)_"),
                    color = 0xB5323A,
                    footer = new { text = $"{string.Join("  ·  ", limits)}  ·  créé par {createdBy}" },
                },
            },
            allowed_mentions = new { parse = Array.Empty<string>() },
        };

        _ = Task.Run(async () =>
        {
            try
            {
                await Http.PostAsJsonAsync(url, payload);
            }
            catch
            {
                // Tir-et-oublie : la création du code a déjà réussi, l'annonce est secondaire.
            }
        });
    }
}
