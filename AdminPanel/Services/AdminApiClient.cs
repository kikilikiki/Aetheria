using System.Net.Http;
using System.Net.Http.Json;
using Aetheria.Shared;
using Aetheria.Shared.Models.Admin;

namespace Aetheria.AdminPanel.Services;

/// <summary>Résultat d'un appel API : soit une valeur, soit un message d'erreur lisible.</summary>
public readonly record struct ApiResult<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;

    public static ApiResult<T> Success(T value) => new(value, null);

    public static ApiResult<T> Failure(string error) => new(default, error);
}

/// <summary>Client HTTP vers les endpoints d'administration exposés par Aetheria.Server.</summary>
public sealed class AdminApiClient : IDisposable
{
    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri($"http://localhost:{GameInfo.DefaultAccountApiPort}"),
        Timeout = TimeSpan.FromSeconds(10),
    };

    public async Task<ApiResult<IReadOnlyList<AdminUserSummary>>> GetUsersAsync(string? search)
    {
        try
        {
            var url = string.IsNullOrWhiteSpace(search)
                ? "/api/admin/users"
                : $"/api/admin/users?search={Uri.EscapeDataString(search)}";

            var users = await _http.GetFromJsonAsync<List<AdminUserSummary>>(url);
            return ApiResult<IReadOnlyList<AdminUserSummary>>.Success(users ?? []);
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<IReadOnlyList<AdminUserSummary>>.Failure($"Impossible de contacter le serveur : {ex.Message}");
        }
    }

    public async Task<ApiResult<AdminGlobalStats>> GetStatsAsync()
    {
        try
        {
            var stats = await _http.GetFromJsonAsync<AdminGlobalStats>("/api/admin/stats");
            return ApiResult<AdminGlobalStats>.Success(stats!);
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<AdminGlobalStats>.Failure($"Impossible de contacter le serveur : {ex.Message}");
        }
    }

    public async Task<ApiResult<bool>> BanAsync(Guid userId, string reason)
        => await PostAsync($"/api/admin/users/{userId}/ban", new BanUserRequest { Reason = reason });

    public async Task<ApiResult<bool>> UnbanAsync(Guid userId)
        => await PostAsync<object?>($"/api/admin/users/{userId}/unban", null);

    private async Task<ApiResult<bool>> PostAsync<T>(string url, T body)
    {
        try
        {
            var response = body is null
                ? await _http.PostAsync(url, null)
                : await _http.PostAsJsonAsync(url, body);

            return response.IsSuccessStatusCode
                ? ApiResult<bool>.Success(true)
                : ApiResult<bool>.Failure($"Erreur serveur ({(int)response.StatusCode}).");
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<bool>.Failure($"Impossible de contacter le serveur : {ex.Message}");
        }
    }

    public void Dispose() => _http.Dispose();
}
