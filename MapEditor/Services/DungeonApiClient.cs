using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheria.Shared;
using Aetheria.Shared.Models;

namespace Aetheria.MapEditor.Services;

/// <summary>Résultat d'un appel API : soit une valeur, soit un message d'erreur lisible.</summary>
public readonly record struct ApiResult<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;

    public static ApiResult<T> Success(T value) => new(value, null);

    public static ApiResult<T> Failure(string error) => new(default, error);
}

/// <summary>Client HTTP vers le catalogue de donjons et de royaumes exposé par Aetheria.Server.</summary>
public sealed class DungeonApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri($"http://localhost:{GameInfo.DefaultAccountApiPort}"),
        Timeout = TimeSpan.FromSeconds(10),
    };

    public async Task<ApiResult<IReadOnlyList<DungeonData>>> GetDungeonsAsync()
    {
        try
        {
            var dungeons = await _http.GetFromJsonAsync<List<DungeonData>>("/api/dungeons", JsonOptions);
            return ApiResult<IReadOnlyList<DungeonData>>.Success(dungeons ?? []);
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<IReadOnlyList<DungeonData>>.Failure($"Impossible de contacter le serveur : {ex.Message}");
        }
    }

    public async Task<ApiResult<IReadOnlyList<KingdomData>>> GetKingdomsAsync()
    {
        try
        {
            var kingdoms = await _http.GetFromJsonAsync<List<KingdomData>>("/api/kingdoms", JsonOptions);
            return ApiResult<IReadOnlyList<KingdomData>>.Success(kingdoms ?? []);
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<IReadOnlyList<KingdomData>>.Failure($"Impossible de contacter le serveur : {ex.Message}");
        }
    }

    public async Task<ApiResult<DungeonFloor>> GetFloorAsync(int dungeonId, int floorNumber)
    {
        try
        {
            var floor = await _http.GetFromJsonAsync<DungeonFloor>(
                $"/api/dungeons/{dungeonId}/floors/{floorNumber}", JsonOptions);
            return ApiResult<DungeonFloor>.Success(floor!);
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<DungeonFloor>.Failure($"Impossible de contacter le serveur : {ex.Message}");
        }
    }

    public async Task<ApiResult<DungeonData>> CreateAsync(DungeonData dungeon)
        => await SendAsync(HttpMethod.Post, "/api/dungeons", dungeon);

    public async Task<ApiResult<DungeonData>> UpdateAsync(int id, DungeonData dungeon)
        => await SendAsync(HttpMethod.Put, $"/api/dungeons/{id}", dungeon);

    public async Task<ApiResult<bool>> DeleteAsync(int id)
    {
        try
        {
            var response = await _http.DeleteAsync($"/api/dungeons/{id}");
            return response.IsSuccessStatusCode
                ? ApiResult<bool>.Success(true)
                : ApiResult<bool>.Failure($"Erreur serveur ({(int)response.StatusCode}).");
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<bool>.Failure($"Impossible de contacter le serveur : {ex.Message}");
        }
    }

    private async Task<ApiResult<DungeonData>> SendAsync(HttpMethod method, string url, DungeonData dungeon)
    {
        try
        {
            var response = await _http.SendAsync(new HttpRequestMessage(method, url)
            {
                Content = JsonContent.Create(dungeon, options: JsonOptions),
            });

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<ApiErrorBody>();
                return ApiResult<DungeonData>.Failure(error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).");
            }

            var body = await response.Content.ReadFromJsonAsync<DungeonData>(JsonOptions);
            return ApiResult<DungeonData>.Success(body!);
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<DungeonData>.Failure($"Impossible de contacter le serveur : {ex.Message}");
        }
    }

    public void Dispose() => _http.Dispose();

    private sealed class ApiErrorBody
    {
        public string? Message { get; init; }
    }
}
