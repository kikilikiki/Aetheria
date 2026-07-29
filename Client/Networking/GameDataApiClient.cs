using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheria.Shared;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Aetheria.Shared.Models.Account;
using Aetheria.Shared.Models.Admin;

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

    /// <summary>Voir GDD/demande utilisateur — vendre à la marchande (moins qu'à l'Hôtel des ventes, voir ShopService.SellAsync).</summary>
    public async Task<ShopPurchaseResponse> SellItemAsync(string sessionToken, Guid characterId, int itemId, int quantity, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/shop/sell", new ShopSellRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            ItemId = itemId,
            Quantity = quantity,
        }, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
            return new ShopPurchaseResponse { Success = false, Message = error?.Message ?? $"Erreur serveur ({(int)response.StatusCode})." };
        }

        var body = await response.Content.ReadFromJsonAsync<ShopPurchaseResponse>(cancellationToken: ct);
        return body!;
    }

    /// <summary>Voir GDD/demande utilisateur — "un HDV où les joueurs mettent en vente et achètent".</summary>
    public async Task<List<AuctionListingSummary>> GetAuctionListingsAsync(Guid viewerCharacterId, CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<AuctionListingSummary>>(
            $"/api/auction/listings?viewerCharacterId={viewerCharacterId}", JsonOptions, ct);
        return result ?? [];
    }

    public async Task<AuctionResponse> CreateAuctionListingAsync(string sessionToken, Guid characterId, int itemId, int quantity, long pricePerUnit, CancellationToken ct = default)
        => await PostAuctionActionAsync("/api/auction/list", new CreateAuctionListingRequest
        {
            SessionToken = sessionToken, CharacterId = characterId, ItemId = itemId, Quantity = quantity, PricePerUnit = pricePerUnit,
        }, ct);

    public async Task<AuctionResponse> BuyAuctionListingAsync(string sessionToken, Guid characterId, Guid listingId, CancellationToken ct = default)
        => await PostAuctionActionAsync("/api/auction/buy", new AuctionActionRequest
        {
            SessionToken = sessionToken, CharacterId = characterId, ListingId = listingId,
        }, ct);

    public async Task<AuctionResponse> CancelAuctionListingAsync(string sessionToken, Guid characterId, Guid listingId, CancellationToken ct = default)
        => await PostAuctionActionAsync("/api/auction/cancel", new AuctionActionRequest
        {
            SessionToken = sessionToken, CharacterId = characterId, ListingId = listingId,
        }, ct);

    private async Task<AuctionResponse> PostAuctionActionAsync<TRequest>(string url, TRequest request, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync(url, request, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
            return new AuctionResponse { Success = false, Message = error?.Message ?? $"Erreur serveur ({(int)response.StatusCode})." };
        }

        var body = await response.Content.ReadFromJsonAsync<AuctionResponse>(cancellationToken: ct);
        return body!;
    }

    /// <summary>Voir GDD/demande utilisateur — "liste des items que l'on peut craft et ce qu'il faut".</summary>
    public async Task<List<RecipeSummary>> GetRecipesAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<RecipeSummary>>("/api/professions/recipes", JsonOptions, ct);
        return result ?? [];
    }

    public async Task<ProfessionActionResponse?> CraftAsync(string sessionToken, Guid characterId, int recipeId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/professions/craft", new CraftRequest
        {
            SessionToken = sessionToken, CharacterId = characterId, RecipeId = recipeId,
        }, JsonOptions, ct);

        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ProfessionActionResponse>(JsonOptions, ct) : null;
    }

    /// <summary>Voir GDD/demande utilisateur — "déplacer ce que l'on a dans notre team" (max 4, voir MonsterCareService.SetActiveTeamAsync).</summary>
    public async Task<MonsterInstanceData?> SetMonsterActiveTeamAsync(string sessionToken, Guid monsterId, bool isInActiveTeam, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/monsters/{monsterId}/set-active-team", new SetMonsterActiveTeamRequest
        {
            SessionToken = sessionToken,
            MonsterId = monsterId,
            IsInActiveTeam = isInActiveTeam,
        }, JsonOptions, ct);

        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<MonsterInstanceData>(JsonOptions, ct) : null;
    }

    /// <summary>Voir GDD/demande utilisateur — "un tutoriel qui force le joueur à faire des quêtes qui lui expliquent le jeu".</summary>
    public async Task<QuestSummary?> GetActiveQuestAsync(Guid characterId, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<QuestSummary>($"/api/quests/active?characterId={characterId}", JsonOptions, ct);

    public async Task CompleteQuestAsync(string sessionToken, Guid characterId, string questName, CancellationToken ct = default)
    {
        await _http.PostAsJsonAsync("/api/quests/complete", new CompleteQuestRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            QuestName = questName,
        }, JsonOptions, ct);
    }

    public async Task<List<KingdomData>> GetKingdomsAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<KingdomData>>("/api/kingdoms", JsonOptions, ct);
        return result ?? [];
    }

    /// <summary>Voir GDD/demande utilisateur — "guerre de territoire... quêtes de minage".</summary>
    public async Task<List<TerritorySummary>> GetTerritoriesAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<TerritorySummary>>("/api/territories", JsonOptions, ct);
        return result ?? [];
    }

    public async Task<List<KingdomWarStanding>> GetWarStandingsAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<KingdomWarStanding>>("/api/kingdoms/wars/standings", JsonOptions, ct);
        return result ?? [];
    }

    public async Task<ShopItem?> GetGatherableItemAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<ShopItem>("/api/items/gatherable", JsonOptions, ct);

    public async Task<ProfessionActionResponse?> GatherAsync(string sessionToken, Guid characterId, int resourceItemId, int territoryId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/professions/gather", new GatherRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            Profession = ProfessionType.Mineur,
            ResourceItemId = resourceItemId,
            TerritoryId = territoryId,
        }, JsonOptions, ct);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ProfessionActionResponse>(JsonOptions, ct);
        }

        var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
        return new ProfessionActionResponse { Profession = ProfessionType.Mineur, Level = 0, Experience = 0, LeveledUp = false, Message = error?.Message ?? "Récolte impossible." };
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

    // Voir GDD/demande utilisateur — "panel admin en jeu (pouvoirs, skins, ban/mute/kick)".
    // Réutilise AdminAuthService côté serveur (même jeton de session que le jeu, pas un jeton
    // admin séparé comme le Launcher) — voir Server/Persistence/AdminAuthService.cs.
    public async Task<AdminGameActionResponse> BroadcastAdminMessageAsync(string sessionToken, string message, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/admin/game/broadcast", new AdminBroadcastRequest { SessionToken = sessionToken, Message = message }, ct);

    public async Task<AdminGameActionResponse> ActivateSignModeAsync(string sessionToken, int durationSeconds, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/admin/game/sign-mode", new AdminSignModeRequest { SessionToken = sessionToken, DurationSeconds = durationSeconds }, ct);

    public async Task<AdminGameActionResponse> GiveItemToPlayerAsync(string sessionToken, string targetCharacterName, int itemId, int quantity, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/admin/game/give-item", new AdminGiveItemRequest
        {
            SessionToken = sessionToken, TargetCharacterName = targetCharacterName, ItemId = itemId, Quantity = quantity,
        }, ct);

    public async Task<AdminGameActionResponse> KickPlayerAsync(string sessionToken, string targetCharacterName, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/admin/game/kick", new AdminKickRequest { SessionToken = sessionToken, TargetCharacterName = targetCharacterName }, ct);

    /// <summary>Voir GDD/demande utilisateur — bouton exclusif au Fondateur.</summary>
    public async Task<AdminGameActionResponse> ToggleAdminAsync(string sessionToken, string targetCharacterName, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/admin/game/toggle-admin", new AdminToggleAdminRequest { SessionToken = sessionToken, TargetCharacterName = targetCharacterName }, ct);

    public async Task<AdminGameActionResponse> LevelUpMonsterAsync(string sessionToken, Guid monsterId, int levels, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/admin/game/level-up-monster", new AdminLevelUpMonsterRequest { SessionToken = sessionToken, MonsterId = monsterId, Levels = levels }, ct);

    private async Task<AdminGameActionResponse> PostAdminActionAsync<TRequest>(string url, TRequest request, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync(url, request, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
            return new AdminGameActionResponse { Success = false, Message = error?.Message ?? $"Erreur serveur ({(int)response.StatusCode})." };
        }

        var body = await response.Content.ReadFromJsonAsync<AdminGameActionResponse>(cancellationToken: ct);
        return body!;
    }

    public void Dispose() => _http.Dispose();
}
