using System.Net.Http;
using System.Net.Http.Json;
using Aetheria.Shared;
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
    private readonly HttpClient _http;

    public AccountApiClient(string? baseUrl = null)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl ?? $"http://localhost:{GameInfo.DefaultAccountApiPort}"),
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
                var error = await response.Content.ReadFromJsonAsync<ApiError>();
                return ApiResult<Guid>.Failure(error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).");
            }

            var body = await response.Content.ReadFromJsonAsync<RegisterOkBody>();
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

    public void Dispose() => _http.Dispose();

    private sealed class RegisterOkBody
    {
        public Guid UserId { get; init; }
    }
}
