using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Aetheria.Shared;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models.Account;
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

    public async Task<ApiResult<LoginResponse>> LoginAsync(string usernameOrEmail, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/api/account/login",
                new LoginRequest { UsernameOrEmail = usernameOrEmail, Password = password });

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<ApiError>();
                return ApiResult<LoginResponse>.Failure(error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).");
            }

            var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
            return ApiResult<LoginResponse>.Success(body!);
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<LoginResponse>.Failure($"Impossible de contacter le serveur : {ex.Message}");
        }
    }

    public async Task<ApiResult<bool>> DeleteUserAsync(Guid userId, string sessionToken)
        => await PostAsync($"/api/admin/users/{userId}/delete", new AdminSessionRequest { SessionToken = sessionToken });

    public async Task<ApiResult<bool>> RestoreUserAsync(Guid userId, string sessionToken)
        => await PostAsync($"/api/admin/users/{userId}/restore", new AdminSessionRequest { SessionToken = sessionToken });

    public async Task<ApiResult<bool>> ModifyUserAsync(Guid userId, string sessionToken, string? newUsername, string? newEmail)
        => await PostAsync($"/api/admin/users/{userId}/modify",
            new AdminModifyUserRequest { SessionToken = sessionToken, NewUsername = newUsername, NewEmail = newEmail });

    public async Task<ApiResult<bool>> SetAdminAsync(Guid userId, string sessionToken, bool isAdmin)
        => await PostAsync($"/api/admin/users/{userId}/set-admin",
            new AdminSetPermissionRequest { SessionToken = sessionToken, IsAdmin = isAdmin });

    public async Task<ApiResult<bool>> SetRankAsync(Guid userId, string sessionToken, UserRank rank)
        => await PostAsync($"/api/admin/users/{userId}/set-rank",
            new AdminSetRankRequest { SessionToken = sessionToken, Rank = rank });

    private async Task<ApiResult<bool>> PostAsync<T>(string url, T body)
    {
        try
        {
            var response = body is null
                ? await _http.PostAsync(url, null)
                : await _http.PostAsJsonAsync(url, body);

            if (response.IsSuccessStatusCode)
            {
                return ApiResult<bool>.Success(true);
            }

            var error = await response.Content.ReadFromJsonAsync<ApiError>();
            return ApiResult<bool>.Failure(error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).");
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<bool>.Failure($"Impossible de contacter le serveur : {ex.Message}");
        }
    }

    public void Dispose() => _http.Dispose();
}
