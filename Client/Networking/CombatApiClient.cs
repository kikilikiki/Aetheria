using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheria.Shared;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models.Account;
using Aetheria.Shared.Models.Combat;

namespace Aetheria.Client.Networking;

/// <summary>Résultat d'un appel combat : soit un nouvel état, soit un message d'erreur lisible.</summary>
public readonly record struct CombatResult(CombatSessionState? State, string? Error)
{
    public bool IsSuccess => Error is null;
}

/// <summary>
/// Client HTTP vers le système de combat tactique (voir <c>Server/World/CombatService.cs</c>).
/// Même remarque que <see cref="StarterApiClient"/> sur PropertyNameCaseInsensitive.
/// </summary>
public sealed class CombatApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _http;

    public CombatApiClient(string host)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri($"http://{host}:{GameInfo.DefaultAccountApiPort}"),
            Timeout = TimeSpan.FromSeconds(10),
        };
    }

    public async Task<CombatResult> StartAsync(string sessionToken, Guid characterId, IReadOnlyList<Guid> monsterIds, int wildSpeciesId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/combat/start", new StartCombatRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            MonsterIds = monsterIds,
            WildSpeciesId = wildSpeciesId,
        }, JsonOptions, ct);

        return await ReadResultAsync(response, ct);
    }

    /// <summary>Rencontre sauvage hors donjon (voir GDD) — le serveur choisit lui-même l'espèce, scalée sur le niveau (voir <c>CombatService.StartWildEncounterAsync</c>).</summary>
    public async Task<CombatResult> StartWildEncounterAsync(string sessionToken, Guid characterId, IReadOnlyList<Guid> monsterIds, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/combat/start-wild", new StartWildEncounterRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            MonsterIds = monsterIds,
        }, JsonOptions, ct);

        return await ReadResultAsync(response, ct);
    }

    public async Task<CombatResult> SubmitActionAsync(
        string sessionToken, Guid combatId, CombatActionType actionType, int targetX, int targetY, int? captureItemId = null, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/combat/{combatId}/action", new CombatActionRequest
        {
            SessionToken = sessionToken,
            ActionType = actionType,
            TargetX = targetX,
            TargetY = targetY,
            CaptureItemId = captureItemId,
        }, JsonOptions, ct);

        return await ReadResultAsync(response, ct);
    }

    private static async Task<CombatResult> ReadResultAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
            return new CombatResult(null, error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).");
        }

        var state = await response.Content.ReadFromJsonAsync<CombatSessionState>(JsonOptions, ct);
        return new CombatResult(state, null);
    }

    /// <summary>Récupère le butin de victoire (voir GDD — 4 objets à départager) associé à <see cref="CombatSessionState.LootId"/>.</summary>
    public async Task<LootSessionState?> GetLootAsync(Guid lootId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/loot/{lootId}", ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<LootSessionState>(JsonOptions, ct) : null;
    }

    public async Task<LootSessionState?> ClaimLootAsync(string sessionToken, Guid lootId, Guid characterId, int itemIndex, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/loot/{lootId}/claim", new LootClaimRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            ItemIndex = itemIndex,
        }, JsonOptions, ct);

        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<LootSessionState>(JsonOptions, ct) : null;
    }

    public void Dispose() => _http.Dispose();
}
