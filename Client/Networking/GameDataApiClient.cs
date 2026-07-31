using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheria.Shared;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Aetheria.Shared.Models.Account;
using Aetheria.Shared.Models.Admin;
using Aetheria.Shared.Models.BattlePass;
using Aetheria.Shared.Models.Premium;
using Aetheria.Shared.Models.WorldBoss;
using Aetheria.Shared.Models.GuildRaid;

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

    public async Task<GuildSummary> CreateGuildAsync(string sessionToken, Guid characterId, string name, bool isPublic = true, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/guilds", new CreateGuildRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            Name = name,
            IsPublic = isPublic,
        }, JsonOptions, ct);

        return await ReadGuildResultAsync(response, ct);
    }

    public async Task<GuildSummary> JoinGuildAsync(string sessionToken, Guid characterId, Guid guildId, string? joinCode = null, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/guilds/{guildId}/join", new JoinGuildRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            JoinCode = joinCode,
        }, JsonOptions, ct);

        return await ReadGuildResultAsync(response, ct);
    }

    public async Task<GuildSummary> DepositGuildGoldAsync(string sessionToken, Guid characterId, Guid guildId, long amount, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/guilds/{guildId}/deposit-gold", new GuildDepositGoldRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            Amount = amount,
        }, JsonOptions, ct);

        return await ReadGuildResultAsync(response, ct);
    }

    /// <summary>Voir GDD/demande utilisateur — "Housing/décoration de guilde ou de royaume".</summary>
    public async Task<List<GuildDecorationDefinition>> GetGuildDecorationCatalogAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<GuildDecorationDefinition>>("/api/guilds/decorations/catalog", JsonOptions, ct);
        return result ?? [];
    }

    public async Task<GuildSummary> PurchaseGuildDecorationAsync(string sessionToken, Guid characterId, Guid guildId, string decorationKey, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/guilds/{guildId}/decorations/purchase", new GuildDecorationActionRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            DecorationKey = decorationKey,
        }, JsonOptions, ct);

        return await ReadGuildResultAsync(response, ct);
    }

    public async Task<GuildSummary> SetActiveGuildDecorationAsync(string sessionToken, Guid characterId, Guid guildId, string decorationKey, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/guilds/{guildId}/decorations/set-active", new GuildDecorationActionRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            DecorationKey = decorationKey,
        }, JsonOptions, ct);

        return await ReadGuildResultAsync(response, ct);
    }

    public async Task<List<GuildChestItemSummary>> GetGuildChestAsync(Guid guildId, CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<GuildChestItemSummary>>($"/api/guilds/{guildId}/chest", JsonOptions, ct);
        return result ?? [];
    }

    public async Task<List<GuildChestItemSummary>> DepositGuildItemAsync(string sessionToken, Guid characterId, Guid guildId, int itemId, int quantity, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/guilds/{guildId}/chest/deposit", new GuildChestActionRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            ItemId = itemId,
            Quantity = quantity,
        }, JsonOptions, ct);

        return await ReadGuildChestResultAsync(response, ct);
    }

    public async Task<List<GuildChestItemSummary>> WithdrawGuildItemAsync(string sessionToken, Guid characterId, Guid guildId, int itemId, int quantity, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/guilds/{guildId}/chest/withdraw", new GuildChestActionRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            ItemId = itemId,
            Quantity = quantity,
        }, JsonOptions, ct);

        return await ReadGuildChestResultAsync(response, ct);
    }

    /// <summary>Voir GDD/demande utilisateur — "Classement" (des guildes).</summary>
    public async Task<List<GuildSummary>> GetGuildLeaderboardAsync(int limit = 10, CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<GuildSummary>>($"/api/guilds/leaderboard?limit={limit}", JsonOptions, ct);
        return result ?? [];
    }

    private static async Task<List<GuildChestItemSummary>> ReadGuildChestResultAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
            throw new HttpRequestException(error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).");
        }

        return (await response.Content.ReadFromJsonAsync<List<GuildChestItemSummary>>(JsonOptions, ct))!;
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
    public async Task<ChestLootResult?> OpenChestAsync(string sessionToken, Guid characterId, int dungeonId, int floorNumber, int roomIndex, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/dungeons/{dungeonId}/floors/{floorNumber}/rooms/{roomIndex}/loot-chest", new OpenChestRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
        }, JsonOptions, ct);

        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ChestLootResult>(JsonOptions, ct) : null;
    }

    /// <summary>Voir GDD/demande utilisateur — "ajoute un cooldown de 1h avant que il puisse retourne dans le dongon ou il vient d'aller".</summary>
    public async Task<DungeonEntryStatus?> GetDungeonEntryStatusAsync(string sessionToken, Guid characterId, int dungeonId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/dungeons/{dungeonId}/entry-status", new DungeonEntryStatusRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
        }, JsonOptions, ct);

        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<DungeonEntryStatus>(JsonOptions, ct) : null;
    }

    /// <summary>Voir GDD/demande utilisateur — "a la fin des 10 etage termine le dongon [...] donne lui des recompense".</summary>
    public async Task<DungeonCompletionResult?> CompleteDungeonAsync(string sessionToken, Guid characterId, int dungeonId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/dungeons/{dungeonId}/complete", new DungeonCompleteRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
        }, JsonOptions, ct);

        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<DungeonCompletionResult>(JsonOptions, ct) : null;
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

    /// <summary>Voir GDD/demande utilisateur — "shop avec des gems".</summary>
    public async Task<PremiumStatus?> GetPremiumStatusAsync(string sessionToken, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/shop/premium/status?sessionToken={Uri.EscapeDataString(sessionToken)}", ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<PremiumStatus>(JsonOptions, ct) : null;
    }

    public async Task<ShopPurchaseResponse> ExchangeGoldForGemsAsync(string sessionToken, Guid characterId, long goldAmount, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/shop/gems/exchange-gold", new ExchangeGoldForGemsRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            GoldAmount = goldAmount,
        }, JsonOptions, ct);

        return await ReadPremiumAsShopResponseAsync(response, ct);
    }

    public async Task<ShopPurchaseResponse> UpgradePremiumGradeAsync(string sessionToken, Guid characterId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/shop/premium/grade/upgrade", new PurchasePremiumTierRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
        }, JsonOptions, ct);

        return await ReadPremiumAsShopResponseAsync(response, ct);
    }

    /// <summary>Les endpoints premium renvoient un <see cref="PremiumStatus"/> en succès — traduit en <see cref="ShopPurchaseResponse"/> pour réutiliser le même affichage de message que le reste de la boutique.</summary>
    private async Task<ShopPurchaseResponse> ReadPremiumAsShopResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
            return new ShopPurchaseResponse { Success = false, Message = error?.Message ?? $"Erreur serveur ({(int)response.StatusCode})." };
        }

        var status = await response.Content.ReadFromJsonAsync<PremiumStatus>(JsonOptions, ct);
        return new ShopPurchaseResponse { Success = true, Message = "Achat réussi.", RemainingGold = status?.Gems ?? 0 };
    }

    /// <summary>Voir GDD/demande utilisateur — "un HDV où les joueurs mettent en vente et achètent".</summary>
    public async Task<List<AuctionListingSummary>> GetAuctionListingsAsync(Guid viewerCharacterId, CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<AuctionListingSummary>>(
            $"/api/auction/listings?viewerCharacterId={viewerCharacterId}", JsonOptions, ct);
        return result ?? [];
    }

    public async Task<AuctionResponse> CreateAuctionListingAsync(string sessionToken, Guid characterId, int itemId, int quantity, long pricePerUnit, bool isAuction = false, CancellationToken ct = default)
        => await PostAuctionActionAsync("/api/auction/list", new CreateAuctionListingRequest
        {
            SessionToken = sessionToken, CharacterId = characterId, ItemId = itemId, Quantity = quantity, PricePerUnit = pricePerUnit, IsAuction = isAuction,
        }, ct);

    /// <summary>Voir GDD/demande utilisateur — "la possibilité de le mettre aux enchères".</summary>
    public async Task<AuctionResponse> PlaceAuctionBidAsync(string sessionToken, Guid characterId, Guid listingId, long bidAmount, CancellationToken ct = default)
        => await PostAuctionActionAsync("/api/auction/bid", new AuctionBidRequest
        {
            SessionToken = sessionToken, CharacterId = characterId, ListingId = listingId, BidAmount = bidAmount,
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

    /// <summary>Voir GDD/demande utilisateur — "un UI avec un bouton pour voir les métiers, les niveaux de chaque métier".</summary>
    public async Task<List<ProfessionSummary>> GetProfessionsAsync(Guid characterId, CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<ProfessionSummary>>($"/api/professions/{characterId}", JsonOptions, ct);
        return result ?? [];
    }

    /// <summary>Voir GDD/demande utilisateur — "un pass de niveaux de joueur ... si il paie le pass premium alors il auront accès à des trucs plus exclusif".</summary>
    public async Task<BattlePassStatus?> GetBattlePassStatusAsync(Guid characterId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/battlepass/{characterId}", ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<BattlePassStatus>(JsonOptions, ct) : null;
    }

    public async Task<ShopPurchaseResponse> PurchaseBattlePassPremiumAsync(string sessionToken, Guid characterId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/battlepass/premium/purchase", new PurchaseBattlePassPremiumRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
        }, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
            return new ShopPurchaseResponse { Success = false, Message = error?.Message ?? $"Erreur serveur ({(int)response.StatusCode})." };
        }

        var status = await response.Content.ReadFromJsonAsync<BattlePassStatus>(JsonOptions, ct);
        return new ShopPurchaseResponse { Success = true, Message = "Pass premium débloqué.", RemainingGold = status?.Level ?? 0 };
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

    /// <summary>Voir GDD/demande utilisateur — "Fonctionnalités de royaume avancées" (élections du roi, taxes, construction).</summary>
    public async Task<KingdomPoliticsStatus?> GetKingdomPoliticsAsync(Guid characterId, CancellationToken ct = default)
    {
        return await _http.GetFromJsonAsync<KingdomPoliticsStatus>($"/api/kingdoms/politics?characterId={characterId}", JsonOptions, ct);
    }

    /// <summary>Voir GDD/demande utilisateur — "contenu end-game".</summary>
    public async Task<EndGameStatus?> GetEndGameStatusAsync(Guid characterId, CancellationToken ct = default)
    {
        return await _http.GetFromJsonAsync<EndGameStatus>($"/api/endgame/status?characterId={characterId}", JsonOptions, ct);
    }

    /// <summary>Voir GDD/demande utilisateur — "Défis hebdomadaires" + défis mensuels.</summary>
    public async Task<List<ChallengeStatus>> GetChallengesAsync(Guid characterId, CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<ChallengeStatus>>($"/api/challenges?characterId={characterId}", JsonOptions, ct);
        return result ?? [];
    }

    public async Task<ChallengeStatus> ClaimChallengeAsync(string sessionToken, Guid characterId, string challengeKey, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/challenges/claim", new ClaimChallengeRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            ChallengeKey = challengeKey,
        }, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
            throw new HttpRequestException(error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).");
        }

        return (await response.Content.ReadFromJsonAsync<ChallengeStatus>(JsonOptions, ct))!;
    }

    /// <summary>Voir GDD/demande utilisateur — "Exploration : coffres cachés hebdomadaires par royaume".</summary>
    public async Task<WeeklyChestStatus?> GetWeeklyChestAsync(int kingdomId, CancellationToken ct = default)
    {
        return await _http.GetFromJsonAsync<WeeklyChestStatus>($"/api/kingdoms/{kingdomId}/weekly-chest", JsonOptions, ct);
    }

    public async Task<WeeklyChestStatus?> ClaimWeeklyChestAsync(string sessionToken, Guid characterId, int kingdomId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/kingdoms/{kingdomId}/weekly-chest/claim?characterId={characterId}", new AdminSessionRequest
        {
            SessionToken = sessionToken,
        }, JsonOptions, ct);

        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<WeeklyChestStatus>(JsonOptions, ct) : null;
    }

    /// <summary>Voir GDD/demande utilisateur — "le coffre de la semaine doit etre cache sur la map" : le client ne connait que le royaume du personnage (KingdomType).</summary>
    public async Task<WeeklyChestStatus?> GetWeeklyChestByKingdomAsync(KingdomType kingdom, CancellationToken ct = default)
    {
        return await _http.GetFromJsonAsync<WeeklyChestStatus>($"/api/kingdoms/by-type/{kingdom}/weekly-chest", JsonOptions, ct);
    }

    public async Task<WeeklyChestStatus?> ClaimWeeklyChestByKingdomAsync(string sessionToken, Guid characterId, KingdomType kingdom, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/kingdoms/by-type/{kingdom}/weekly-chest/claim?characterId={characterId}", new AdminSessionRequest
        {
            SessionToken = sessionToken,
        }, JsonOptions, ct);

        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<WeeklyChestStatus>(JsonOptions, ct) : null;
    }

    /// <summary>Voir GDD/demande utilisateur — "Exploration : îles volantes/aquatiques + montures dédiées".</summary>
    public async Task<string?> VisitIslandAsync(string sessionToken, Guid characterId, MountKind islandKind, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/exploration/visit-island", new VisitIslandRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            IslandKind = islandKind,
        }, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
            throw new HttpRequestException(error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).");
        }

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return body.TryGetProperty("message", out var messageProp) ? messageProp.GetString() : null;
    }

    public async Task<bool> VoteForKingAsync(string sessionToken, Guid characterId, string candidateName, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/kingdoms/vote", new VoteForKingRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            CandidateName = candidateName,
        }, JsonOptions, ct);

        return response.IsSuccessStatusCode;
    }

    public async Task<KingdomPoliticsStatus?> ConstructKingdomBuildingAsync(string sessionToken, Guid characterId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/kingdoms/construct", new ConstructKingdomBuildingRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
        }, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<KingdomPoliticsStatus>(JsonOptions, ct);
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

    /// <summary>Voir GDD/demande utilisateur — "ajoute des bâtiments dans les villes (mine, champs etc)" : ressource du Champ, pendant de <see cref="GetGatherableItemAsync"/> pour la Mine.</summary>
    public async Task<ShopItem?> GetGatherableCropItemAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<ShopItem>("/api/items/gatherable-crop", JsonOptions, ct);

    /// <summary>Récolte au Champ (voir GetGatherableCropItemAsync) — même mécanique de capture/contrôle de territoire que la Mine (voir GatherAsync).</summary>
    public async Task<ProfessionActionResponse?> GatherCropAsync(string sessionToken, Guid characterId, int resourceItemId, int territoryId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/professions/gather", new GatherRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            Profession = ProfessionType.Agriculteur,
            ResourceItemId = resourceItemId,
            TerritoryId = territoryId,
        }, JsonOptions, ct);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ProfessionActionResponse>(JsonOptions, ct);
        }

        var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
        return new ProfessionActionResponse { Profession = ProfessionType.Agriculteur, Level = 0, Experience = 0, LeveledUp = false, Message = error?.Message ?? "Récolte impossible." };
    }

    /// <summary>Voir GDD/demande utilisateur — "Prestige après niveau maximum".</summary>
    public async Task<MonsterInstanceData?> PrestigeMonsterAsync(string sessionToken, Guid characterId, Guid monsterId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/monsters/{monsterId}/prestige", new PrestigeMonsterRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            MonsterId = monsterId,
        }, JsonOptions, ct);

        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<MonsterInstanceData>(JsonOptions, ct) : null;
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

    /// <summary>Voir GDD/demande utilisateur — "on peut changer la compétence avec un objet" (Parchemin de Compétence).</summary>
    public async Task<MonsterInstanceData?> RerollPassiveTalentAsync(string sessionToken, Guid monsterId, int itemId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/monsters/reroll-passive", new RerollPassiveTalentRequest
        {
            SessionToken = sessionToken,
            MonsterId = monsterId,
            ItemId = itemId,
        }, JsonOptions, ct);

        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<MonsterInstanceData>(JsonOptions, ct) : null;
    }

    /// <summary>Voir GDD/demande utilisateur — "ajoute un item pour changer les iv" (Pierre de Réinitialisation).</summary>
    public async Task<MonsterInstanceData?> RerollIvAsync(string sessionToken, Guid monsterId, int itemId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/monsters/reroll-iv", new RerollIvRequest
        {
            SessionToken = sessionToken,
            MonsterId = monsterId,
            ItemId = itemId,
        }, JsonOptions, ct);

        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<MonsterInstanceData>(JsonOptions, ct) : null;
    }

    /// <summary>Voir GDD/demande utilisateur — "Talents/capacités passives uniques par monstre (comme les 'natures' Pokémon)".</summary>
    public async Task<MonsterInstanceData?> RerollNatureAsync(string sessionToken, Guid monsterId, int itemId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/monsters/reroll-nature", new RerollNatureRequest
        {
            SessionToken = sessionToken,
            MonsterId = monsterId,
            ItemId = itemId,
        }, JsonOptions, ct);

        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<MonsterInstanceData>(JsonOptions, ct) : null;
    }

    /// <summary>Voir GDD/demande utilisateur — "les items équipés peuvent donner des avantages à nos monstres".</summary>
    public async Task<MonsterInstanceData?> EquipItemAsync(string sessionToken, Guid monsterId, int itemId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/monsters/{monsterId}/equip", new EquipItemRequest { SessionToken = sessionToken, ItemId = itemId }, JsonOptions, ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<MonsterInstanceData>(JsonOptions, ct) : null;
    }

    public async Task<MonsterInstanceData?> UnequipItemAsync(string sessionToken, Guid monsterId, EquipmentSlot slot, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/monsters/{monsterId}/unequip", new UnequipItemRequest { SessionToken = sessionToken, Slot = slot }, JsonOptions, ct);
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

    /// <summary>Voir GDD/demande utilisateur — "ajoute une commande et un champ admin pour donner des palier a un joueur" (paliers du Passe de Niveau).</summary>
    public async Task<AdminGameActionResponse> GiveBattlePassLevelAsync(string sessionToken, string targetCharacterName, int levels, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/admin/game/give-battlepass-level", new AdminGiveBattlePassLevelRequest { SessionToken = sessionToken, TargetCharacterName = targetCharacterName, Levels = levels }, ct);

    /// <summary>Voir GDD/demande utilisateur — "ajoute une commande pour give des montures".</summary>
    public async Task<AdminGameActionResponse> GiveMountAsync(string sessionToken, string targetCharacterName, string mountKey, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/admin/game/give-mount", new AdminGiveMountRequest { SessionToken = sessionToken, TargetCharacterName = targetCharacterName, MountKey = mountKey }, ct);

    public async Task<AdminGameActionResponse> LevelUpMonsterAsync(string sessionToken, Guid monsterId, int levels, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/admin/game/level-up-monster", new AdminLevelUpMonsterRequest { SessionToken = sessionToken, MonsterId = monsterId, Levels = levels }, ct);

    public async Task<AdminGameActionResponse> BanPlayerAsync(string sessionToken, string targetCharacterName, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/admin/game/ban", new AdminBanRequest { SessionToken = sessionToken, TargetCharacterName = targetCharacterName }, ct);

    public async Task<AdminGameActionResponse> TransformPlayerAsync(string sessionToken, string targetCharacterName, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/admin/game/transform", new AdminTransformRequest { SessionToken = sessionToken, TargetCharacterName = targetCharacterName, DurationSeconds = 60 }, ct);

    public async Task<AdminGameActionResponse> GiveMonsterToPlayerAsync(string sessionToken, string targetCharacterName, int speciesId, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/admin/game/give-monster", new AdminGiveMonsterRequest { SessionToken = sessionToken, TargetCharacterName = targetCharacterName, SpeciesId = speciesId }, ct);

    public async Task<AdminGameActionResponse> MaxLevelTeamAsync(string sessionToken, string targetCharacterName, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/admin/game/max-level-team", new AdminMaxLevelTeamRequest { SessionToken = sessionToken, TargetCharacterName = targetCharacterName }, ct);

    // Voir retour utilisateur — "il manque des commandes dans les commandes admin (F2)" : existaient
    // déjà en commande de tchat (/givemoney, /givexp, /setlevel, /unban, /givegems) mais pas ici.
    public async Task<AdminGameActionResponse> GiveMoneyAsync(string sessionToken, string targetCharacterName, long amount, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/admin/game/give-money", new AdminGiveMoneyRequest { SessionToken = sessionToken, TargetCharacterName = targetCharacterName, Amount = amount }, ct);

    public async Task<AdminGameActionResponse> GiveXpAsync(string sessionToken, string targetCharacterName, long amount, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/admin/game/give-xp", new AdminGiveXpRequest { SessionToken = sessionToken, TargetCharacterName = targetCharacterName, Amount = amount }, ct);

    public async Task<AdminGameActionResponse> SetLevelAsync(string sessionToken, string targetCharacterName, int level, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/admin/game/set-level", new AdminSetLevelRequest { SessionToken = sessionToken, TargetCharacterName = targetCharacterName, Level = level }, ct);

    public async Task<AdminGameActionResponse> UnbanCharacterAsync(string sessionToken, string targetCharacterName, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/admin/game/unban", new AdminUnbanCharacterRequest { SessionToken = sessionToken, TargetCharacterName = targetCharacterName }, ct);

    /// <summary>Réservé au grade Fondateur — le serveur revérifie de toute façon (voir /api/admin/game/give-gems).</summary>
    public async Task<AdminGameActionResponse> GiveGemsAsync(string sessionToken, string targetCharacterName, long amount, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/admin/game/give-gems", new AdminGiveGemsRequest { SessionToken = sessionToken, TargetCharacterName = targetCharacterName, Amount = amount }, ct);

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

    /// <summary>Voir GDD/demande utilisateur — "un bouton pour le leaderboard en jeu et sur le launcher".</summary>
    public async Task<List<LeaderboardRow>> GetLeaderboardAsync(LeaderboardCategory category, int limit = 10, CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<LeaderboardRow>>($"/api/leaderboard/{category}?limit={limit}", JsonOptions, ct);
        return result ?? [];
    }

    public Task RefreshLeaderboardAsync(LeaderboardCategory category, CancellationToken ct = default) =>
        _http.PostAsync($"/api/leaderboard/{category}/refresh", null, ct);

    /// <summary>Voir GDD/demande utilisateur — "classement de team, visible seulement si on est dans la même équipe" : le royaume est résolu côté serveur à partir de sessionToken/characterId, jamais envoyé par le client.</summary>
    public async Task<List<LeaderboardRow>> GetKingdomLeaderboardAsync(LeaderboardCategory category, string sessionToken, Guid characterId, int limit = 5, CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<LeaderboardRow>>(
            $"/api/leaderboard/{category}/kingdom?sessionToken={Uri.EscapeDataString(sessionToken)}&characterId={characterId}&limit={limit}", JsonOptions, ct);
        return result ?? [];
    }

    /// <summary>Voir GDD/demande utilisateur — "indicateurs visuels quand double XP/loot sont actifs" : pas de session requise, statut public interrogé périodiquement (voir hudPollClock côté Client).</summary>
    public async Task<GlobalEventStatus?> GetGlobalEventStatusAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/api/game/events/status", ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<GlobalEventStatus>(JsonOptions, ct) : null;
    }

    /// <summary>Voir GDD/demande utilisateur — "un endroit pour modifier son profil".</summary>
    public async Task<ProfileSummary?> GetProfileAsync(Guid characterId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/profile/{characterId}", ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ProfileSummary>(JsonOptions, ct) : null;
    }

    public async Task<ProfileSummary?> UpdateProfileAsync(string sessionToken, Guid characterId, string description, int? showcaseItemId, string? activeTitle, string? activeMountKey = null, string? activeWingKey = null, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/profile/update", new UpdateProfileRequest
        {
            SessionToken = sessionToken, CharacterId = characterId, Description = description, ShowcaseItemId = showcaseItemId, ActiveTitle = activeTitle,
            ActiveMountKey = activeMountKey, ActiveWingKey = activeWingKey,
        }, JsonOptions, ct);

        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ProfileSummary>(JsonOptions, ct) : null;
    }

    /// <summary>Voir GDD/demande utilisateur — "ajouter les amis".</summary>
    public async Task<List<FriendSummary>> GetFriendsAsync(Guid characterId, CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<FriendSummary>>($"/api/friends/{characterId}", JsonOptions, ct);
        return result ?? [];
    }

    public async Task<List<FriendRequestSummary>> GetPendingFriendRequestsAsync(Guid characterId, CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<FriendRequestSummary>>($"/api/friends/{characterId}/pending", JsonOptions, ct);
        return result ?? [];
    }

    public async Task<AdminGameActionResponse> SendFriendRequestAsync(string sessionToken, Guid characterId, string targetCharacterName, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/friends/request", new FriendActionRequest { SessionToken = sessionToken, CharacterId = characterId, TargetCharacterName = targetCharacterName }, ct);

    public async Task<AdminGameActionResponse> RespondFriendRequestAsync(string sessionToken, Guid characterId, string requesterCharacterName, bool accept, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/friends/respond", new FriendRespondRequest { SessionToken = sessionToken, CharacterId = characterId, RequesterCharacterName = requesterCharacterName, Accept = accept }, ct);

    public async Task<AdminGameActionResponse> RemoveFriendAsync(string sessionToken, Guid characterId, string targetCharacterName, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/friends/remove", new FriendActionRequest { SessionToken = sessionToken, CharacterId = characterId, TargetCharacterName = targetCharacterName }, ct);

    /// <summary>Voir GDD/demande utilisateur — "Système d'échange (trade) entre joueurs".</summary>
    public async Task<List<TradeOfferSummary>> GetIncomingTradeOffersAsync(Guid characterId, CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<TradeOfferSummary>>($"/api/trade/{characterId}/incoming", JsonOptions, ct);
        return result ?? [];
    }

    public async Task<List<TradeOfferSummary>> GetOutgoingTradeOffersAsync(Guid characterId, CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<TradeOfferSummary>>($"/api/trade/{characterId}/outgoing", JsonOptions, ct);
        return result ?? [];
    }

    public async Task<AdminGameActionResponse> ProposeTradeAsync(string sessionToken, Guid characterId, string targetCharacterName, Guid? offeredMonsterId, long offeredGold, long requestedGold, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/trade/propose", new ProposeTradeRequest
        {
            SessionToken = sessionToken,
            InitiatorCharacterId = characterId,
            TargetCharacterName = targetCharacterName,
            OfferedMonsterId = offeredMonsterId,
            OfferedGold = offeredGold,
            RequestedGold = requestedGold,
        }, ct);

    public async Task<AdminGameActionResponse> RespondTradeAsync(string sessionToken, Guid characterId, Guid offerId, bool accept, CancellationToken ct = default)
        => await PostAdminActionAsync($"/api/trade/{offerId}/respond", new RespondTradeRequest { SessionToken = sessionToken, CharacterId = characterId, Accept = accept }, ct);

    // Voir GDD/demande utilisateur — "un batiment pour fusionner des monstres" + retour
    // utilisateur "ajoute un temps et une validation avant de le faire" : en deux temps
    // (start puis claim une fois le délai écoulé, voir GetPendingFusionAsync pour le sondage).
    public async Task<PendingFusionStatus> StartFusionAsync(string sessionToken, Guid characterId, Guid survivorMonsterId, Guid consumedMonsterId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/monsters/fuse/start", new FuseMonstersRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            SurvivorMonsterId = survivorMonsterId,
            ConsumedMonsterId = consumedMonsterId,
        }, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
            throw new HttpRequestException(error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).");
        }

        return (await response.Content.ReadFromJsonAsync<PendingFusionStatus>(JsonOptions, ct))!;
    }

    public async Task<PendingFusionStatus?> GetPendingFusionAsync(string sessionToken, Guid characterId, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<PendingFusionStatus>($"/api/monsters/fuse/status?sessionToken={Uri.EscapeDataString(sessionToken)}&characterId={characterId}", JsonOptions, ct);

    public async Task<MonsterInstanceData> ClaimFusionAsync(string sessionToken, Guid characterId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/monsters/fuse/claim", new ClaimPendingMonsterRequest { SessionToken = sessionToken, CharacterId = characterId }, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
            throw new HttpRequestException(error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).");
        }

        return (await response.Content.ReadFromJsonAsync<MonsterInstanceData>(JsonOptions, ct))!;
    }

    /// <summary>Voir GDD/demande utilisateur — "un batiment pour faire de la reproduction avec heritage de statistiques" + retour utilisateur — "ajoute un temps et une validation avant de le faire" : en deux temps (start puis claim).</summary>
    public async Task<PendingBreedStatus> StartBreedAsync(string sessionToken, Guid characterId, Guid parentMonsterId1, Guid parentMonsterId2, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/monsters/breed/start", new BreedMonstersRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            ParentMonsterId1 = parentMonsterId1,
            ParentMonsterId2 = parentMonsterId2,
        }, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
            throw new HttpRequestException(error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).");
        }

        return (await response.Content.ReadFromJsonAsync<PendingBreedStatus>(JsonOptions, ct))!;
    }

    public async Task<PendingBreedStatus?> GetPendingBreedAsync(string sessionToken, Guid characterId, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<PendingBreedStatus>($"/api/monsters/breed/status?sessionToken={Uri.EscapeDataString(sessionToken)}&characterId={characterId}", JsonOptions, ct);

    public async Task<MonsterInstanceData> ClaimBreedAsync(string sessionToken, Guid characterId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/monsters/breed/claim", new ClaimPendingMonsterRequest { SessionToken = sessionToken, CharacterId = characterId }, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
            throw new HttpRequestException(error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).");
        }

        return (await response.Content.ReadFromJsonAsync<MonsterInstanceData>(JsonOptions, ct))!;
    }

    // Voir GDD/demande utilisateur — "un boss monde... barre de vie... leaderboard".
    public async Task<WorldBossStatus?> GetWorldBossStatusAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/api/worldboss/status", ct);
        return response.StatusCode == System.Net.HttpStatusCode.NoContent
            ? null
            : await response.Content.ReadFromJsonAsync<WorldBossStatus>(JsonOptions, ct);
    }

    public async Task<WorldBossAttackResponse> AttackWorldBossAsync(string sessionToken, Guid characterId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/worldboss/attack", new WorldBossAttackRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
        }, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
            return new WorldBossAttackResponse(false, error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).", 0, 0, false, 0);
        }

        return (await response.Content.ReadFromJsonAsync<WorldBossAttackResponse>(JsonOptions, ct))!;
    }

    public async Task<List<WorldBossLeaderboardRow>> GetWorldBossLeaderboardAsync(bool allTime, int limit = 10, CancellationToken ct = default)
    {
        var scope = allTime ? "alltime" : "current";
        var result = await _http.GetFromJsonAsync<List<WorldBossLeaderboardRow>>($"/api/worldboss/leaderboard?scope={scope}&limit={limit}", JsonOptions, ct);
        return result ?? [];
    }

    /// <summary>Voir GDD/demande utilisateur — "Raids de guilde (boss coopératif nécessitant plusieurs joueurs, distinct du world boss solo/petit groupe)".</summary>
    public async Task<GuildRaidStatus?> GetGuildRaidStatusAsync(Guid characterId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/guildraid/status/{characterId}", ct);
        return response.StatusCode == System.Net.HttpStatusCode.NoContent
            ? null
            : await response.Content.ReadFromJsonAsync<GuildRaidStatus>(JsonOptions, ct);
    }

    public async Task<AdminGameActionResponse> SpawnGuildRaidAsync(string sessionToken, Guid characterId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/guildraid/spawn", new GuildRaidSpawnRequest { SessionToken = sessionToken, CharacterId = characterId }, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
            return new AdminGameActionResponse { Success = false, Message = error?.Message ?? $"Erreur serveur ({(int)response.StatusCode})." };
        }

        var status = await response.Content.ReadFromJsonAsync<GuildRaidStatus>(JsonOptions, ct);
        return new AdminGameActionResponse { Success = true, Message = $"{status?.Name} invoqué !" };
    }

    public async Task<GuildRaidAttackResponse> AttackGuildRaidAsync(string sessionToken, Guid characterId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/guildraid/attack", new GuildRaidAttackRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
        }, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
            return new GuildRaidAttackResponse(false, error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).", 0, 0, false, 0);
        }

        return (await response.Content.ReadFromJsonAsync<GuildRaidAttackResponse>(JsonOptions, ct))!;
    }

    public async Task<List<GuildRaidLeaderboardRow>> GetGuildRaidLeaderboardAsync(Guid characterId, int limit = 10, CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<GuildRaidLeaderboardRow>>($"/api/guildraid/leaderboard/{characterId}?limit={limit}", JsonOptions, ct);
        return result ?? [];
    }

    /// <summary>Voir GDD/demande utilisateur — "retire le champ espece et royaume pour le boss monde" : espece tiree au sort cote serveur, plus de royaume cible. Reserve aux admins/fondateur (le serveur reverifie de toute facon).</summary>
    public async Task<AdminGameActionResponse> SpawnWorldBossAsync(string sessionToken, int maxHealth, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/admin/game/spawn-world-boss", new SpawnWorldBossRequest { SessionToken = sessionToken, MaxHealth = maxHealth }, ct);

    // Voir GDD/demande utilisateur — "commandes admin abuse : double XP, double butin, invasion de monstres".
    public async Task<AdminGameActionResponse> ActivateDoubleXpAsync(string sessionToken, int durationMinutes, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/admin/game/double-xp", new AdminGlobalEventRequest { SessionToken = sessionToken, DurationMinutes = durationMinutes }, ct);

    public async Task<AdminGameActionResponse> ActivateDoubleLootAsync(string sessionToken, int durationMinutes, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/admin/game/double-loot", new AdminGlobalEventRequest { SessionToken = sessionToken, DurationMinutes = durationMinutes }, ct);

    public async Task<AdminGameActionResponse> ActivateInvasionAsync(string sessionToken, KingdomType kingdom, int durationMinutes, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/admin/game/invasion", new AdminInvasionRequest { SessionToken = sessionToken, Kingdom = kingdom, DurationMinutes = durationMinutes }, ct);

    /// <summary>Voir retour utilisateur — "ajouter un admin pour desactiver les combats".</summary>
    public async Task<AdminGameActionResponse> ToggleCombatsDisabledAsync(string sessionToken, CancellationToken ct = default)
        => await PostAdminActionAsync("/api/admin/game/toggle-combats", new AdminToggleCombatsRequest { SessionToken = sessionToken }, ct);

    /// <summary>Voir GDD/demande utilisateur — "les admin peut voir les report... sur un ui que seul les admin peuvent voir".</summary>
    public async Task<List<ReportSummary>> GetReportsAsync(string sessionToken, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<ReportSummary>>($"/api/admin/reports?sessionToken={Uri.EscapeDataString(sessionToken)}", JsonOptions, ct) ?? [];

    public async Task<AdminGameActionResponse> ResolveReportAsync(string sessionToken, Guid reportId, CancellationToken ct = default)
        => await PostAdminActionAsync($"/api/admin/reports/{reportId}/resolve", new AdminSessionRequest { SessionToken = sessionToken }, ct);

    /// <summary>Voir GDD/demande utilisateur — "la possibilité de se téléporter a la personne qui a report et a la personne qui a été report".</summary>
    public async Task<PlayerLocationSummary?> LocatePlayerAsync(string sessionToken, string characterName, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/admin/locate/{Uri.EscapeDataString(characterName)}?sessionToken={Uri.EscapeDataString(sessionToken)}", ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<PlayerLocationSummary>(JsonOptions, ct) : null;
    }

    public void Dispose() => _http.Dispose();
}
