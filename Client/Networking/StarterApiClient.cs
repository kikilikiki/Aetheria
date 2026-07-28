using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheria.Shared;
using Aetheria.Shared.Models;
using Aetheria.Shared.Models.Account;

namespace Aetheria.Client.Networking;

/// <summary>
/// Client HTTP minimal vers l'API de compte (voir <c>Server/Program.cs</c>), utilisé uniquement
/// pour la scène de sélection du premier compagnon (voir <c>Docs/GameDesign.md</c>). Le reste du
/// jeu communique par le protocole TCP de <see cref="GameConnection"/> ; ce petit client HTTP
/// séparé évite d'alourdir ce protocole pour un échange ponctuel au tout début de partie.
/// </summary>
public sealed class StarterApiClient : IDisposable
{
    // PropertyNameCaseInsensitive est requis explicitement ici : les méthodes pratiques de
    // System.Net.Http.Json (GetFromJsonAsync, PostAsJsonAsync, ...) utilisent par défaut des
    // options insensibles à la casse quand on ne leur passe RIEN — mais dès qu'on fournit ses
    // propres JsonSerializerOptions (ici pour le JsonStringEnumConverter), ce défaut disparaît.
    // Sans cette ligne, les propriétés "required" (Id, Name, ...) ne se lient pas face au JSON
    // camelCase du serveur et la désérialisation lève une JsonException — bug reproduit en test.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _http;

    public StarterApiClient(string host)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri($"http://{host}:{GameInfo.DefaultAccountApiPort}"),
            Timeout = TimeSpan.FromSeconds(10),
        };
    }

    public async Task<List<MonsterSpeciesData>> GetStarterSpeciesAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<MonsterSpeciesData>>("/api/monsters/species/starters", JsonOptions, ct);
        return result ?? [];
    }

    public async Task<List<MonsterInstanceData>> GetCharacterMonstersAsync(Guid characterId, CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<MonsterInstanceData>>($"/api/characters/{characterId}/monsters", JsonOptions, ct);
        return result ?? [];
    }

    public async Task<StarterChoiceResponse> ChooseStarterAsync(string sessionToken, Guid characterId, int speciesId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/characters/{characterId}/starter", new StarterChoiceRequest
        {
            SessionToken = sessionToken,
            CharacterId = characterId,
            SpeciesId = speciesId,
        }, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
            return new StarterChoiceResponse { Success = false, Message = error?.Message ?? $"Erreur serveur ({(int)response.StatusCode})." };
        }

        var body = await response.Content.ReadFromJsonAsync<StarterChoiceResponse>(cancellationToken: ct);
        return body!;
    }

    public void Dispose() => _http.Dispose();
}
