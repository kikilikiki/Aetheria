using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models.Account;

namespace Aetheria.Launcher.Services;

/// <summary>Résultat d'un appel à l'API de compte : soit une valeur, soit un message d'erreur lisible.</summary>
public readonly record struct ApiResult<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;

    public static ApiResult<T> Success(T value) => new(value, null);

    public static ApiResult<T> Failure(string error) => new(default, error);
}

/// <summary>Client HTTP vers l'API de compte exposée par Aetheria.Server (voir Server/Program.cs).</summary>
public sealed class AccountApiClient : IDisposable
{
    // Le serveur sérialise les enums en toutes lettres ("Feu", "Guerrier", ...) via
    // ConfigureHttpJsonOptions (voir Server/Program.cs) — sans ce même JsonStringEnumConverter
    // ici, ReadFromJsonAsync<LoginResponse> échoue dès qu'un personnage existant est renvoyé
    // (ex. LoginResponse.Characters[].Kingdom) avec "The JSON value could not be converted to
    // Aetheria.Shared.Enums.KingdomType" — bug reproduit en connexion sur un compte non vide.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _http;

    public AccountApiClient(string baseUrl)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(10),
        };
    }

    public async Task<ApiResult<Guid>> RegisterAsync(string username, string email, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/api/account/register",
                new RegisterRequest { Username = username, Email = email, Password = password });

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
                return ApiResult<Guid>.Failure(error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).");
            }

            var body = await response.Content.ReadFromJsonAsync<RegisterOkBody>(JsonOptions);
            return ApiResult<Guid>.Success(body!.UserId);
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<Guid>.Failure($"Impossible de contacter le serveur : {ex.Message}");
        }
    }

    public async Task<ApiResult<LoginResponse>> LoginAsync(string usernameOrEmail, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/api/account/login",
                new LoginRequest { UsernameOrEmail = usernameOrEmail, Password = password });

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
                return ApiResult<LoginResponse>.Failure(error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).");
            }

            var body = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
            return ApiResult<LoginResponse>.Success(body!);
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<LoginResponse>.Failure($"Impossible de contacter le serveur : {ex.Message}");
        }
    }

    /// <summary>Voir Docs/Idees.md — vraie image de profil : upload multipart vers <c>POST /api/account/avatar</c>, retourne la nouvelle URL relative en cas de succès.</summary>
    public async Task<ApiResult<string>> UploadAvatarAsync(string sessionToken, byte[] fileBytes, string fileName)
    {
        try
        {
            using var content = new MultipartFormDataContent
            {
                { new StringContent(sessionToken), "sessionToken" },
            };
            var fileContent = new ByteArrayContent(fileBytes);
            content.Add(fileContent, "avatar", fileName);

            var response = await _http.PostAsync("/api/account/avatar", content);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
                return ApiResult<string>.Failure(error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).");
            }

            var body = await response.Content.ReadFromJsonAsync<AvatarUploadOkBody>(JsonOptions);
            return ApiResult<string>.Success(body!.AvatarUrl);
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<string>.Failure($"Impossible de contacter le serveur : {ex.Message}");
        }
    }

    private sealed class AvatarUploadOkBody
    {
        public string AvatarUrl { get; init; } = string.Empty;
    }

    /// <summary>Revalide un jeton de session persisté (voir GDD/demande utilisateur — "rester connecté jusqu'à la déconnexion"), sans redemander les identifiants.</summary>
    public async Task<ApiResult<SessionInfoResponse>> ValidateSessionAsync(string sessionToken)
    {
        try
        {
            var response = await _http.GetAsync($"/api/account/session?sessionToken={Uri.EscapeDataString(sessionToken)}");
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
                return ApiResult<SessionInfoResponse>.Failure(error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).");
            }

            var body = await response.Content.ReadFromJsonAsync<SessionInfoResponse>(JsonOptions);
            return ApiResult<SessionInfoResponse>.Success(body!);
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<SessionInfoResponse>.Failure($"Impossible de contacter le serveur : {ex.Message}");
        }
    }

    /// <summary>Voir GDD/demande utilisateur — "un bouton pour le leaderboard en jeu et sur le launcher".</summary>
    public async Task<ApiResult<List<Aetheria.Shared.Models.LeaderboardRow>>> GetLeaderboardAsync(LeaderboardCategory category, int limit = 10)
    {
        try
        {
            var response = await _http.GetAsync($"/api/leaderboard/{category}?limit={limit}");
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
                return ApiResult<List<Aetheria.Shared.Models.LeaderboardRow>>.Failure(error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).");
            }

            var body = await response.Content.ReadFromJsonAsync<List<Aetheria.Shared.Models.LeaderboardRow>>(JsonOptions);
            return ApiResult<List<Aetheria.Shared.Models.LeaderboardRow>>.Success(body ?? []);
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<List<Aetheria.Shared.Models.LeaderboardRow>>.Failure($"Impossible de contacter le serveur : {ex.Message}");
        }
    }

    public void Dispose() => _http.Dispose();

    private sealed class RegisterOkBody
    {
        public Guid UserId { get; init; }
    }
}
