using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/exploration/visit-island</c> — voir GDD/demande utilisateur "îles volantes (monture volante requise) et îles aquatiques (monture aquatique requise)".</summary>
public sealed class VisitIslandRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required MountKind IslandKind { get; init; }
}
