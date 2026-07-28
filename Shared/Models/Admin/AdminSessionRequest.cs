namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON des actions admin qui ne modifient rien d'autre que l'authentification (ex. suppression).</summary>
public sealed class AdminSessionRequest
{
    public required string SessionToken { get; init; }
}
