using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheria.Shared;
using Aetheria.Shared.Models;
using Aetheria.Shared.Models.Account;

namespace Aetheria.Client.Networking;

/// <summary>
/// Client HTTP pour les panneaux en jeu (Inventaire, Guilde, Boutique — voir GDD). Même remarque
/// que <see cref="StarterApiClient"/> sur PropertyNameCaseInsensitive.
/// </summary>
public sealed class GameDataApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _http;

    public GameDataApiClient(string apiBaseUrl)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(apiBaseUrl),
            Timeout = TimeSpan.FromSeconds(10),
        };
    }

    public async Task<List<InventoryItemSummary>> GetInventoryAsync(Guid characterId, CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<InventoryItemSummary>>($"/api/characters/{characterId}/inventory", JsonOptions, ct);
        return result ?? [];
    }

    public async Task<GuildSummary?> GetMyGuildAsync(Guid characterId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/guilds/mine?characterId={characterId}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<GuildSummary>(JsonOptions, ct);
    }

    /// <summary>Recherche de guildes par nom (voir GDD — panneau Guilde). Toutes les guildes si <paramref name="search"/> est vide.</summary>
    public async Task<List<GuildSummary>> SearchGuildsAsync(string? search, CancellationToken ct = default)
    {
        var query = string.IsNullOrWhiteSpace(search) ? string.Empty : $"?search={Uri.EscapeDataString(search)}";
        var result = await _http.GetFromJsonAsync<List<GuildSummary>>($"/api/guilds{query}", JsonOptions, ct);
        return result ?? [];
    }

    public async Task<GuildSummary> CreateGuildAsync(string sessionToken, Guid characterId, string name, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/guilds", new CreateGuildRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            Name = name,
        }, JsonOptions, ct);

        return await ReadGuildResultAsync(response, ct);
    }

    public async Task<GuildSummary> JoinGuildAsync(string sessionToken, Guid characterId, Guid guildId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/guilds/{guildId}/join", new JoinGuildRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
        }, JsonOptions, ct);

        return await ReadGuildResultAsync(response, ct);
    }

    private static async Task<GuildSummary> ReadGuildResultAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
            throw new HttpRequestException(error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).");
        }

        return (await response.Content.ReadFromJsonAsync<GuildSummary>(JsonOptions, ct))!;
    }

    public async Task<PartySummary?> GetMyPartyAsync(Guid characterId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/parties/mine?characterId={characterId}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<PartySummary>(JsonOptions, ct);
    }

    public async Task<PartySummary> CreatePartyAsync(string sessionToken, Guid characterId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/parties", new CreatePartyRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
        }, JsonOptions, ct);

        return await ReadPartyResultAsync(response, ct);
    }

    public async Task<PartySummary> JoinPartyAsync(string sessionToken, Guid characterId, string joinCode, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/parties/join", new JoinPartyRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            JoinCode = joinCode,
        }, JsonOptions, ct);

        return await ReadPartyResultAsync(response, ct);
    }

    public async Task LeavePartyAsync(string sessionToken, Guid characterId, CancellationToken ct = default)
    {
        await _http.PostAsJsonAsync("/api/parties/leave", new LeavePartyRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
        }, JsonOptions, ct);
    }

    private static async Task<PartySummary> ReadPartyResultAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
            throw new HttpRequestException(error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).");
        }

        return (await response.Content.ReadFromJsonAsync<PartySummary>(JsonOptions, ct))!;
    }

    /// <summary>Catalogue complet des espèces (voir GDD — UI de gestion des montres, résolution du nom d'affichage).</summary>
    public async Task<List<MonsterSpeciesData>> GetAllSpeciesAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<MonsterSpeciesData>>("/api/monsters/species", JsonOptions, ct);
        return result ?? [];
    }

    public async Task<List<DungeonData>> GetDungeonsAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<DungeonData>>("/api/dungeons", JsonOptions, ct);
        return result ?? [];
    }

    /// <summary>Séquence de salles d'un étage de donjon (voir GDD — exploration en couloir linéaire).</summary>
    public async Task<DungeonFloor?> GetDungeonFloorAsync(int dungeonId, int floorNumber, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/dungeons/{dungeonId}/floors/{floorNumber}", ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<DungeonFloor>(JsonOptions, ct) : null;
    }

    /// <summary>Aperçu de la créature d'une salle avant de l'affronter (voir GDD/demande utilisateur — "voir les ennemis avant de les combattre"), ou <c>null</c> si la salle ne contient pas de monstre.</summary>
    public async Task<MonsterSpeciesData?> GetDungeonEncounterPreviewAsync(int dungeonId, int floorNumber, int roomIndex, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/dungeons/{dungeonId}/floors/{floorNumber}/rooms/{roomIndex}/encounter-preview", ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<MonsterSpeciesData>(JsonOptions, ct) : null;
    }

    /// <summary>Ouvre une salle Coffre (voir GDD — "loot au fil du chemin") ; retourne l'or gagné, ou <c>null</c> en cas d'échec.</summary>
    public async Task<int?> OpenChestAsync(string sessionToken, Guid characterId, int dungeonId, int floorNumber, int roomIndex, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/dungeons/{dungeonId}/floors/{floorNumber}/rooms/{roomIndex}/loot-chest", new OpenChestRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
        }, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return body.GetProperty("goldGained").GetInt32();
    }

    public async Task<List<ShopItem>> GetShopCatalogAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<ShopItem>>("/api/shop/catalog", JsonOptions, ct);
        return result ?? [];
    }

    public async Task<ShopPurchaseResponse> BuyItemAsync(string sessionToken, Guid characterId, int itemId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/shop/buy", new ShopPurchaseRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            ItemId = itemId,
        }, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
            return new ShopPurchaseResponse { Success = false, Message = error?.Message ?? $"Erreur serveur ({(int)response.StatusCode})." };
        }

        var body = await response.Content.ReadFromJsonAsync<ShopPurchaseResponse>(cancellationToken: ct);
        return body!;
    }

    /// <summary>Donne un objet d'inventaire à une créature (voir GDD — UI de gestion des montres).</summary>
    public async Task<MonsterInstanceData?> GiveItemToMonsterAsync(string sessionToken, Guid monsterId, int itemId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/monsters/{monsterId}/give-item", new GiveItemToMonsterRequest
        {
            SessionToken = sessionToken,
            MonsterId = monsterId,
            ItemId = itemId,
        }, JsonOptions, ct);

        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<MonsterInstanceData>(JsonOptions, ct) : null;
    }

    public void Dispose() => _http.Dispose();
}
