namespace Aetheria.Database.Entities;

/// <summary>
/// Adresse IP bannie (voir GDD/demande utilisateur — "ban ip") : distincte du bannissement de
/// compte (<c>UserEntity.IsBanned</c>), bloque la connexion depuis cette IP quel que soit le
/// compte utilisé. Vérifiée à la connexion (voir <c>AccountService.LoginAsync</c>).
/// </summary>
public sealed class BannedIpEntity
{
    public Guid Id { get; set; }
    public required string IpAddress { get; set; }
    public string? Reason { get; set; }
    public DateTime BannedAtUtc { get; set; } = DateTime.UtcNow;
}
