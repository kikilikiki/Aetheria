namespace Aetheria.Shared.Models.Account;

/// <summary>Réponse JSON d'erreur uniforme pour l'API de compte (400/401/409...).</summary>
public sealed class ApiError
{
    public required string Message { get; init; }
}
