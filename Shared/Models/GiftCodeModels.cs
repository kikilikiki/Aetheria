namespace Aetheria.Shared.Models;

/// <summary>Corps de <c>POST /api/giftcodes/redeem</c> (utilisé par le Launcher).</summary>
public sealed class RedeemGiftCodeRequest
{
    public required string SessionToken { get; init; }
    public required string Code { get; init; }

    /// <summary>Personnage qui reçoit l'or / la créature éventuels du code (le Launcher passe le personnage actif).</summary>
    public Guid? CharacterId { get; init; }
}

/// <summary>Réponse de <c>POST /api/giftcodes/redeem</c>.</summary>
public sealed class RedeemGiftCodeResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}
