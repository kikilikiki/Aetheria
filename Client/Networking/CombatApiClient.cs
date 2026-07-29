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

    public CombatApiClient(string apiBaseUrl)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(apiBaseUrl),
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

    /// <summary>Engage le combat contre le monstre d'une salle de donjon précise (voir GDD — exploration en couloir linéaire).</summary>
    public async Task<CombatResult> StartDungeonCombatAsync(
        string sessionToken, Guid characterId, IReadOnlyList<Guid> monsterIds, int dungeonId, int floorNumber, int roomIndex, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/dungeons/{dungeonId}/floors/{floorNumber}/rooms/{roomIndex}/engage", new StartDungeonCombatRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            MonsterIds = monsterIds,
        }, JsonOptions, ct);

        return await ReadResultAsync(response, ct);
    }

    /// <summary>Voir GDD/demande utilisateur — "ajouter les demandes en duel pour le pvp" : appelé par le client du défieur une fois l'invitation acceptée (voir DuelAcceptedPacket), après avoir récupéré les créatures de l'adversaire via l'endpoint public des personnages.</summary>
    public async Task<CombatResult> ChallengeAsync(
        string sessionToken, Guid characterId, IReadOnlyList<Guid> monsterIds, Guid opponentCharacterId, IReadOnlyList<Guid> opponentMonsterIds, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/pvp/challenge", new StartPvpCombatRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            MonsterIds = monsterIds,
            OpponentCharacterId = opponentCharacterId,
            OpponentMonsterIds = opponentMonsterIds,
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

    /// <summary>Photographie d'un combat déjà démarré — utilisé après un appairage d'arène (voir GDD) pour récupérer l'état créé par le joueur qui a complété la file d'attente.</summary>
    public async Task<CombatSessionState?> GetStateAsync(Guid combatId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/combat/{combatId}", ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<CombatSessionState>(JsonOptions, ct) : null;
    }

    /// <summary>File d'attente d'arène classée (voir GDD — formats 1v1/2v2/3v3/4v4, ligues ELO).</summary>
    public async Task<bool> QueueForArenaAsync(string sessionToken, Guid characterId, IReadOnlyList<Guid> monsterIds, ArenaFormat format, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/pvp/arena/queue", new QueueForArenaRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            MonsterIds = monsterIds,
            Format = format,
        }, JsonOptions, ct);

        return response.IsSuccessStatusCode;
    }

    public async Task<ArenaQueueStatus?> GetArenaStatusAsync(Guid characterId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/pvp/arena/status?characterId={characterId}", ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ArenaQueueStatus>(JsonOptions, ct) : null;
    }

    public Task CancelArenaQueueAsync(Guid characterId, CancellationToken ct = default) =>
        _http.PostAsync($"/api/pvp/arena/cancel?characterId={characterId}", null, ct);

    public void Dispose() => _http.Dispose();
}
