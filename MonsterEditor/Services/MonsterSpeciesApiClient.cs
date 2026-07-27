using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheria.Shared;
using Aetheria.Shared.Models;

namespace Aetheria.MonsterEditor.Services;

/// <summary>Résultat d'un appel API : soit une valeur, soit un message d'erreur lisible.</summary>
public readonly record struct ApiResult<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;

    public static ApiResult<T> Success(T value) => new(value, null);

    public static ApiResult<T> Failure(string error) => new(default, error);
}

/// <summary>Client HTTP vers le catalogue de monstres exposé par Aetheria.Server (voir Server/Program.cs).</summary>
public sealed class MonsterSpeciesApiClient : IDisposable
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

    public async Task<ApiResult<IReadOnlyList<MonsterSpeciesData>>> GetAllAsync()
    {
        try
        {
            var species = await _http.GetFromJsonAsync<List<MonsterSpeciesData>>("/api/monsters/species", JsonOptions);
            return ApiResult<IReadOnlyList<MonsterSpeciesData>>.Success(species ?? []);
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<IReadOnlyList<MonsterSpeciesData>>.Failure($"Impossible de contacter le serveur : {ex.Message}");
        }
    }

    public async Task<ApiResult<MonsterSpeciesData>> CreateAsync(MonsterSpeciesData species)
        => await SendAsync(HttpMethod.Post, "/api/monsters/species", species);

    public async Task<ApiResult<MonsterSpeciesData>> UpdateAsync(int id, MonsterSpeciesData species)
        => await SendAsync(HttpMethod.Put, $"/api/monsters/species/{id}", species);

    public async Task<ApiResult<bool>> DeleteAsync(int id)
    {
        try
        {
            var response = await _http.DeleteAsync($"/api/monsters/species/{id}");
            return response.IsSuccessStatusCode
                ? ApiResult<bool>.Success(true)
                : ApiResult<bool>.Failure($"Erreur serveur ({(int)response.StatusCode}).");
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<bool>.Failure($"Impossible de contacter le serveur : {ex.Message}");
        }
    }

    private async Task<ApiResult<MonsterSpeciesData>> SendAsync(HttpMethod method, string url, MonsterSpeciesData species)
    {
        try
        {
            var response = await _http.SendAsync(new HttpRequestMessage(method, url)
            {
                Content = JsonContent.Create(species, options: JsonOptions),
            });

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<ApiErrorBody>();
                return ApiResult<MonsterSpeciesData>.Failure(error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).");
            }

            var body = await response.Content.ReadFromJsonAsync<MonsterSpeciesData>(JsonOptions);
            return ApiResult<MonsterSpeciesData>.Success(body!);
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<MonsterSpeciesData>.Failure($"Impossible de contacter le serveur : {ex.Message}");
        }
    }

    public void Dispose() => _http.Dispose();

    private sealed class ApiErrorBody
    {
        public string? Message { get; init; }
    }
}
