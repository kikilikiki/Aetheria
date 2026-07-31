using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models.Account;
using Aetheria.Shared.Models.Admin;

namespace Aetheria.Launcher.Services;

/// <summary>
/// Client HTTP vers les endpoints d'administration exposés par Aetheria.Server, utilisé par le
/// panneau "Communauté" du Launcher (voir GDD/demande utilisateur — "le tout peut aussi se faire
/// via le launcher [...] seulement pour les admin/fondateur") — réservé aux comptes admin/
/// fondateur (voir <c>MainViewModel.IsAdminAccount</c>, vérifié aussi côté serveur via
/// <c>AdminAuthService</c>). Même modèle que <c>AdminPanel/Services/AdminApiClient.cs</c>, gardé
/// séparé plutôt que partagé entre les deux projets WPF pour ne pas leur faire référencer l'un
/// l'autre pour un client HTTP aussi simple.
/// </summary>
public sealed class AdminApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _http;

    public AdminApiClient(string baseUrl)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(10),
        };
    }

    public async Task<ApiResult<IReadOnlyList<AdminUserSummary>>> GetUsersAsync(string? search)
    {
        try
        {
            var url = string.IsNullOrWhiteSpace(search)
                ? "/api/admin/users"
                : $"/api/admin/users?search={Uri.EscapeDataString(search)}";

            var users = await _http.GetFromJsonAsync<List<AdminUserSummary>>(url, JsonOptions);
            return ApiResult<IReadOnlyList<AdminUserSummary>>.Success(users ?? []);
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<IReadOnlyList<AdminUserSummary>>.Failure($"Impossible de contacter le serveur : {ex.Message}");
        }
    }

    public async Task<ApiResult<bool>> BanAsync(Guid userId, string reason)
        => await PostAsync($"/api/admin/users/{userId}/ban", new BanUserRequest { Reason = reason });

    public async Task<ApiResult<bool>> UnbanAsync(Guid userId)
        => await PostAsync<object?>($"/api/admin/users/{userId}/unban", null);

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

    public async Task<ApiResult<bool>> SetMuteAsync(Guid userId, string sessionToken, bool isMuted)
        => await PostAsync($"/api/admin/users/{userId}/set-mute",
            new AdminSetMuteRequest { SessionToken = sessionToken, IsMuted = isMuted });

    public async Task<ApiResult<bool>> BanIpAsync(Guid userId, string sessionToken)
        => await PostAsync($"/api/admin/users/{userId}/ban-ip", new AdminSessionRequest { SessionToken = sessionToken });

    public async Task<ApiResult<bool>> ResetProfileAsync(Guid userId, string sessionToken)
        => await PostAsync($"/api/admin/users/{userId}/reset-profile", new AdminSessionRequest { SessionToken = sessionToken });

    /// <summary>Voir GDD/demande utilisateur — "les admin peut voir les report sur une page sur le launcher". Contrairement à GetUsersAsync ci-dessus, revérifié côté serveur (voir AdminAuthService), d'où le sessionToken requis ici.</summary>
    public async Task<ApiResult<IReadOnlyList<ReportSummary>>> GetReportsAsync(string sessionToken)
    {
        try
        {
            var url = $"/api/admin/reports?sessionToken={Uri.EscapeDataString(sessionToken)}";
            var reports = await _http.GetFromJsonAsync<List<ReportSummary>>(url, JsonOptions);
            return ApiResult<IReadOnlyList<ReportSummary>>.Success(reports ?? []);
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<IReadOnlyList<ReportSummary>>.Failure($"Impossible de contacter le serveur : {ex.Message}");
        }
    }

    public async Task<ApiResult<bool>> ResolveReportAsync(string sessionToken, Guid reportId)
        => await PostAsync($"/api/admin/reports/{reportId}/resolve", new AdminSessionRequest { SessionToken = sessionToken });

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

            var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
            return ApiResult<bool>.Failure(error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).");
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<bool>.Failure($"Impossible de contacter le serveur : {ex.Message}");
        }
    }

    public void Dispose() => _http.Dispose();
}
