using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheria.Shared;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models.Account;

namespace Aetheria.Client.Networking;

/// <summary>
/// Client HTTP vers l'API de compte pour la sélection/création de personnage EN JEU (voir
/// <c>Docs/GameDesign.md</c> — la création ne se fait plus dans le Launcher). Même remarque que
/// <see cref="StarterApiClient"/> : PropertyNameCaseInsensitive est nécessaire dès qu'on fournit
/// ses propres JsonSerializerOptions, sans quoi les propriétés "required" ne se lient pas face
/// au JSON camelCase du serveur (bug reproduit et documenté dans StarterApiClient.cs).
/// </summary>
public sealed class CharacterApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _http;

    public CharacterApiClient(string apiBaseUrl)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(apiBaseUrl),
            Timeout = TimeSpan.FromSeconds(10),
        };
    }

    public async Task<List<CharacterSummary>> GetMyCharactersAsync(string sessionToken, CancellationToken ct = default)
    {
        var url = $"/api/characters/mine?sessionToken={Uri.EscapeDataString(sessionToken)}";
        var result = await _http.GetFromJsonAsync<List<CharacterSummary>>(url, JsonOptions, ct);
        return result ?? [];
    }

    public async Task<CreateCharacterResult> CreateCharacterAsync(CreateCharacterRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/characters", request, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions, ct);
            return new CreateCharacterResult(false, null, error?.Message ?? $"Erreur serveur ({(int)response.StatusCode}).");
        }

        var body = await response.Content.ReadFromJsonAsync<CharacterSummary>(JsonOptions, ct);
        return new CreateCharacterResult(true, body, null);
    }

    public void Dispose() => _http.Dispose();
}

public readonly record struct CreateCharacterResult(bool Success, CharacterSummary? Character, string? Error);
