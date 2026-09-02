namespace Aetheria.Database.Entities;

/// <summary>
/// Trace qu'un compte a utilisé un <see cref="GiftCodeEntity"/> — un même compte ne peut utiliser
/// un code qu'une seule fois (index unique sur <c>GiftCodeId</c> + <c>UserId</c>).
/// </summary>
public sealed class GiftCodeRedemptionEntity
{
    public Guid Id { get; set; }

    public Guid GiftCodeId { get; set; }
    public GiftCodeEntity? GiftCode { get; set; }

    /// <summary>Code au moment de la rédemption (dénormalisé, affichage admin sans jointure).</summary>
    public string Code { get; set; } = string.Empty;

    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }

    /// <summary>Où le code a été saisi : « site » ou « launcher ».</summary>
    public string Source { get; set; } = string.Empty;

    public DateTime RedeemedAtUtc { get; set; } = DateTime.UtcNow;
}
