namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/monsters/{id}/talents/unlock</c> — voir Docs/Idees.md, arbre de talents.</summary>
public sealed class UnlockTalentNodeRequest
{
    public required string SessionToken { get; init; }
    public required string NodeKey { get; init; }
}
