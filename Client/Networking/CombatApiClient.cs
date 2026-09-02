using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheria.Shared;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Aetheria.Shared.Models.Account;
using Aetheria.Shared.Models.Admin;
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

    /// <summary>Voir GDD/demande utilisateur — "on peut attaquer plusieurs fois le boss monde, limite le a 3 et fait que sa soit un vrai combat" (voir <c>CombatService.StartWorldBossEncounterAsync</c>).</summary>
    public async Task<CombatResult> StartWorldBossEncounterAsync(string sessionToken, Guid characterId, IReadOnlyList<Guid> monsterIds, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/worldboss/start-combat", new StartWildEncounterRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            MonsterIds = monsterIds,
        }, JsonOptions, ct);

        return await ReadResultAsync(response, ct);
    }

    /// <summary>Engage le combat contre le monstre d'une salle de donjon précise (voir GDD — exploration en couloir linéaire).</summary>
    public async Task<CombatResult> StartDungeonCombatAsync(
        string sessionToken, Guid characterId, IReadOnlyList<Guid> monsterIds, int dungeonId, int floorNumber, int roomIndex, DungeonModifier modifier = DungeonModifier.Normal, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/dungeons/{dungeonId}/floors/{floorNumber}/rooms/{roomIndex}/engage", new StartDungeonCombatRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            MonsterIds = monsterIds,
            Modifier = modifier,
        }, JsonOptions, ct);

        return await ReadResultAsync(response, ct);
    }

    /// <summary>Voir demande utilisateur — commande/panneau admin "faire apparaître un combat" contre une espèce/variante/niveau choisis.</summary>
    public async Task<CombatResult> SpawnAdminEncounterAsync(
        string sessionToken, Guid characterId, IReadOnlyList<Guid> monsterIds, int speciesId, MonsterVariant variant, int level, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/admin/game/spawn-encounter", new AdminSpawnEncounterRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            MonsterIds = monsterIds,
            SpeciesId = speciesId,
            Variant = variant,
            Level = level,
        }, JsonOptions, ct);

        return await ReadResultAsync(response, ct);
    }

    /// <summary>Voir GDD/demande utilisateur — "propose un pvp, si la personne est en team tout les membres doivent accepter" : appelé par le client du défieur une fois <see cref="Aetheria.Shared.Network.Packets.TeamDuelReadyPacket"/> reçu (tous les membres de l'équipe ciblée ont accepté). Chaque personnage engage son équipe active — pas de sélection manuelle, voir <c>CombatService.StartFriendlyTeamDuelAsync</c>.</summary>
    public async Task<CombatResult> ChallengeTeamAsync(
        string sessionToken, Guid characterId, IReadOnlyList<Guid> challengerTeamCharacterIds, IReadOnlyList<Guid> targetTeamCharacterIds, bool ranked = false, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/pvp/team-challenge", new StartFriendlyTeamDuelRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            ChallengerTeamCharacterIds = challengerTeamCharacterIds,
            TargetTeamCharacterIds = targetTeamCharacterIds,
            Ranked = ranked,
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

    /// <summary>Voir GDD/demande utilisateur — bâtiment "Guerre", UI "prêt" : matchmaking contre un personnage d'un autre royaume (voir KingdomWarQueueService).</summary>
    public async Task<bool> QueueForWarAsync(string sessionToken, Guid characterId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/kingdoms/wars/queue", new QueueForWarRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
        }, JsonOptions, ct);

        return response.IsSuccessStatusCode;
    }

    public async Task<ArenaQueueStatus?> GetWarQueueStatusAsync(Guid characterId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/kingdoms/wars/queue/status?characterId={characterId}", ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ArenaQueueStatus>(JsonOptions, ct) : null;
    }

    public Task CancelWarQueueAsync(Guid characterId, CancellationToken ct = default) =>
        _http.PostAsync($"/api/kingdoms/wars/queue/cancel?characterId={characterId}", null, ct);

    /// <summary>Voir Docs/Idees.md — "PvP sauvage" : rejoint la file (refusé côté serveur si le personnage n'est pas physiquement dans une zone à risque, voir Server/Program.cs IsInWildPvpRiskZone) — <c>Error</c> non-null si refusé.</summary>
    public async Task<(bool Success, string? Error)> QueueForWildPvpAsync(string sessionToken, Guid characterId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/pvp/wild/queue", new QueueForWarRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
        }, JsonOptions, ct);

        if (response.IsSuccessStatusCode)
        {
            return (true, null);
        }

        var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
        return (false, error?.Message ?? "Connexion au serveur impossible.");
    }

    public async Task<ArenaQueueStatus?> GetWildPvpQueueStatusAsync(Guid characterId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/pvp/wild/queue/status?characterId={characterId}", ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ArenaQueueStatus>(JsonOptions, ct) : null;
    }

    public Task CancelWildPvpQueueAsync(Guid characterId, CancellationToken ct = default) =>
        _http.PostAsync($"/api/pvp/wild/queue/cancel?characterId={characterId}", null, ct);

    public async Task<MilitaryReputationStatus?> GetMilitaryReputationAsync(Guid characterId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/pvp/wild/reputation?characterId={characterId}", ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<MilitaryReputationStatus>(JsonOptions, ct) : null;
    }

    /// <summary>Voir GDD/demande utilisateur — "Guerres de guildes" : même mécanique que la guerre de royaumes, matchmaking entre deux guildes différentes.</summary>
    public async Task<bool> QueueForGuildWarAsync(string sessionToken, Guid characterId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/guilds/wars/queue", new QueueForWarRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
        }, JsonOptions, ct);

        return response.IsSuccessStatusCode;
    }

    public async Task<ArenaQueueStatus?> GetGuildWarQueueStatusAsync(Guid characterId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/guilds/wars/queue/status?characterId={characterId}", ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ArenaQueueStatus>(JsonOptions, ct) : null;
    }

    public Task CancelGuildWarQueueAsync(Guid characterId, CancellationToken ct = default) =>
        _http.PostAsync($"/api/guilds/wars/queue/cancel?characterId={characterId}", null, ct);

    /// <summary>Voir GDD/demande utilisateur — "ajoute un leaderboard dans l'UI pour le ready, pour afficher le nombre de points par team".</summary>
    public async Task<List<KingdomWarStanding>> GetWarStandingsAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<KingdomWarStanding>>("/api/kingdoms/wars/standings", JsonOptions, ct);
        return result ?? [];
    }

    public void Dispose() => _http.Dispose();
}
